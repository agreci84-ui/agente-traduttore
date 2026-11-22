<#
.SYNOPSIS
    Crea l'installer di Traduttore dal vivo per Windows, firmato o non firmato.

.DESCRIPTION
    Pubblica il programma completo di runtime .NET (sui PC di destinazione non serve installare altro)
    e lo impacchetta con Inno Setup 6 in installer\out\TraduttoreLive-Setup-<versione>.exe.

    Con -Certificato o -ArtifactSigning firma, con SHA-256 e marca temporale:
      - il programma e tutte le librerie che non hanno già una firma valida;
      - l'installer e il programma di disinstallazione.
    Gli strumenti di firma (signtool e, per Azure, il client di Artifact Signing) vengono scaricati da NuGet
    la prima volta in installer\out\strumenti, e usati solo se hanno una firma valida di Microsoft.

    Serve Inno Setup 6:  winget install --id JRSoftware.InnoSetup -e --scope user

.PARAMETER Versione
    Versione dell'installer, per esempio 1.1.0. Se manca si usa quella di TraduttoreLive.csproj.

.PARAMETER Certificato
    Impronta (thumbprint) di un certificato di firma del codice presente nell'archivio Personale dell'utente
    (certificato su chiavetta o in cloud, se il fornitore lo rende visibile in Windows). Per trovarla:
    Get-ChildItem Cert:\CurrentUser\My -CodeSigningCert

.PARAMETER ArtifactSigning
    Percorso del file JSON con i dati dell'account Azure Artifact Signing:
    { "Endpoint": "https://weu.codesigning.azure.net/", "CodeSigningAccountName": "...", "CertificateProfileName": "..." }
    Prima accedi ad Azure, per esempio con:  az login

.PARAMETER MarcaTemporale
    Server della marca temporale. Predefinito: DigiCert con -Certificato, Microsoft con -ArtifactSigning.

.PARAMETER Fase
    Tutto (predefinito): pubblica il programma e crea l'installer.
    Pubblica: solo il programma, in installer\out\publish.
    Installer: solo l'installer, dai file già in installer\out\publish.
    Le due fasi separate servono a GitHub Actions per far firmare a SignPath i file del programma
    prima di creare l'installer (vedi .github\workflows\build.yml).

.EXAMPLE
    .\installer\crea-installer.ps1
.EXAMPLE
    .\installer\crea-installer.ps1 -Certificato 0123456789ABCDEF0123456789ABCDEF01234567
.EXAMPLE
    .\installer\crea-installer.ps1 -ArtifactSigning C:\firma\metadata.json -Versione 1.1.0
#>
param(
    [string]$Versione,
    [string]$Certificato,
    [string]$ArtifactSigning,
    [string]$MarcaTemporale,
    [ValidateSet('Tutto', 'Pubblica', 'Installer')]
    [string]$Fase = 'Tutto'
)

$ErrorActionPreference = 'Stop'
$progetto = Split-Path $PSScriptRoot -Parent
$csproj = Join-Path $progetto 'TraduttoreLive.csproj'
$uscita = Join-Path $PSScriptRoot 'out'
$pubblicazione = Join-Path $uscita 'publish'
$strumenti = Join-Path $uscita 'strumenti'

# Versioni degli strumenti di firma scaricati da NuGet.
$VersioneSdkBuildTools = '10.0.28000.2957'
$VersioneArtifactSigning = '1.0.151'

# Scarica un pacchetto NuGet (una volta sola) e restituisce il percorso di un suo file per x64,
# controllando che sia firmato da Microsoft.
function Get-StrumentoNuGet([string]$Pacchetto, [string]$VersionePacchetto, [string]$File) {
    $cartella = Join-Path $strumenti "$Pacchetto.$VersionePacchetto"
    if (-not (Test-Path $cartella)) {
        Write-Host "   scarico $Pacchetto $VersionePacchetto da NuGet"
        New-Item -ItemType Directory -Force $strumenti | Out-Null
        $id = $Pacchetto.ToLowerInvariant()
        $nupkg = Join-Path $strumenti "$id.$VersionePacchetto.nupkg"
        [Net.ServicePointManager]::SecurityProtocol = [Net.ServicePointManager]::SecurityProtocol -bor [Net.SecurityProtocolType]::Tls12
        Invoke-WebRequest -UseBasicParsing -OutFile $nupkg `
            "https://api.nuget.org/v3-flatcontainer/$id/$VersionePacchetto/$id.$VersionePacchetto.nupkg"
        Add-Type -AssemblyName System.IO.Compression.FileSystem
        [System.IO.Compression.ZipFile]::ExtractToDirectory($nupkg, $cartella)
        Remove-Item $nupkg
    }
    $trovato = Get-ChildItem $cartella -Recurse -Filter $File | Where-Object { $_.FullName -match '\\x64\\' } | Select-Object -First 1
    if (-not $trovato) { throw "$File non trovato nel pacchetto $Pacchetto $VersionePacchetto." }
    $firma = Get-AuthenticodeSignature $trovato.FullName
    if ($firma.Status -ne 'Valid' -or $firma.SignerCertificate.Subject -notmatch 'O=Microsoft Corporation') {
        throw "$($trovato.FullName) non ha una firma valida di Microsoft: non lo uso. Cancella $cartella e riprova."
    }
    return $trovato.FullName
}

# ---- Versione ----

if (-not $Versione) {
    $Versione = ([xml](Get-Content $csproj -Raw)).Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1
}
if ($Versione -notmatch '^\d+\.\d+\.\d+$') { throw "Versione non valida: '$Versione' (serve per esempio 1.0.0)." }

# ---- Inno Setup ----

$iscc = @(
    "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe",
    "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
    "$env:ProgramFiles\Inno Setup 6\ISCC.exe"
) | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $iscc -and $Fase -ne 'Pubblica') {
    throw "Inno Setup 6 non trovato. Installalo con:  winget install --id JRSoftware.InnoSetup -e --scope user"
}
if ($Fase -eq 'Installer' -and -not (Test-Path (Join-Path $pubblicazione 'TraduttoreLive.exe'))) {
    throw "Manca il programma pubblicato in ${pubblicazione}: prima esegui lo script con -Fase Pubblica."
}

# ---- Firma: controlli prima di compilare, così un errore non fa perdere tempo ----

$signtool = $null
$argomentiFirma = @()
if ($Certificato -and $ArtifactSigning) { throw "Scegli un solo modo di firma: -Certificato oppure -ArtifactSigning." }
if ($Certificato -or $ArtifactSigning) {
    Write-Host "== Preparo la firma" -ForegroundColor Cyan
    $signtool = Get-StrumentoNuGet 'Microsoft.Windows.SDK.BuildTools' $VersioneSdkBuildTools 'signtool.exe'

    if ($Certificato) {
        $impronta = ($Certificato -replace '[^0-9A-Fa-f]', '').ToUpperInvariant()
        $cert = Get-ChildItem Cert:\CurrentUser\My | Where-Object { $_.Thumbprint -eq $impronta } | Select-Object -First 1
        if (-not $cert) {
            throw "Nessun certificato con impronta $impronta nell'archivio Personale. Elenco:  Get-ChildItem Cert:\CurrentUser\My -CodeSigningCert"
        }
        if (-not $cert.HasPrivateKey) { throw "Il certificato $($cert.Subject) non ha la chiave privata su questo PC (chiavetta collegata?)." }
        if ($cert.NotAfter -lt (Get-Date)) { throw "Il certificato $($cert.Subject) è scaduto il $($cert.NotAfter)." }
        if (-not ($cert.EnhancedKeyUsageList | Where-Object { $_.ObjectId -eq '1.3.6.1.5.5.7.3.3' })) {
            throw "Il certificato $($cert.Subject) non è un certificato di firma del codice."
        }
        Write-Host "   certificato: $($cert.Subject) (scade il $($cert.NotAfter.ToString('d')))"
        if (-not $MarcaTemporale) { $MarcaTemporale = 'http://timestamp.digicert.com' }
        $argomentiFirma = @('sign', '/fd', 'SHA256', '/tr', $MarcaTemporale, '/td', 'SHA256', '/s', 'My', '/sha1', $impronta)
    }
    else {
        if (-not (Test-Path $ArtifactSigning)) { throw "File di Artifact Signing non trovato: $ArtifactSigning" }
        $metadati = (Resolve-Path $ArtifactSigning).Path
        $json = Get-Content $metadati -Raw | ConvertFrom-Json
        foreach ($campo in 'Endpoint', 'CodeSigningAccountName', 'CertificateProfileName') {
            if (-not $json.$campo) { throw "Nel file $metadati manca il campo $campo." }
        }
        $dlib = Get-StrumentoNuGet 'Microsoft.ArtifactSigning.Client' $VersioneArtifactSigning 'Azure.CodeSigning.Dlib.dll'
        Write-Host "   Azure Artifact Signing: account $($json.CodeSigningAccountName), profilo $($json.CertificateProfileName)"
        if (-not $MarcaTemporale) { $MarcaTemporale = 'http://timestamp.acs.microsoft.com' }
        $argomentiFirma = @('sign', '/fd', 'SHA256', '/tr', $MarcaTemporale, '/td', 'SHA256', '/dlib', $dlib, '/dmdf', $metadati)
    }
}

# ---- Pubblicazione ----

if ($Fase -ne 'Installer') {
    Write-Host "== Pubblico Traduttore dal vivo $Versione (win-x64, con runtime .NET incluso)" -ForegroundColor Cyan
    if (Test-Path $pubblicazione) { Remove-Item $pubblicazione -Recurse -Force }
    dotnet publish $csproj -c Release -r win-x64 --self-contained true "-p:Version=$Versione" -o $pubblicazione
    if ($LASTEXITCODE -ne 0) { throw "dotnet publish non riuscito." }

    # Whisper.net copia le librerie native di tutte le piattaforme (Linux, Windows a 32 bit e ARM):
    # il programma è a 64 bit, quindi servono solo quelle win-x64.
    Get-ChildItem (Join-Path $pubblicazione 'runtimes') -Directory -Recurse |
        Where-Object { $_.Name -like '*-*' -and $_.Name -ne 'win-x64' } |
        Remove-Item -Recurse -Force -ErrorAction SilentlyContinue
}

# ---- Firma dei file del programma ----

if ($signtool -and $Fase -ne 'Installer') {
    # Il runtime .NET è già firmato da Microsoft; si firmano il programma e le librerie senza firma valida
    # (anche quelle di terze parti: Smart App Control blocca qualsiasi libreria non firmata).
    $daFirmare = @(Get-ChildItem $pubblicazione -Recurse -Include *.exe, *.dll |
        Where-Object { (Get-AuthenticodeSignature $_.FullName).Status -ne 'Valid' } |
        ForEach-Object { $_.FullName })
    Write-Host "== Firmo $($daFirmare.Count) file del programma" -ForegroundColor Cyan
    for ($i = 0; $i -lt $daFirmare.Count; $i += 40) {
        $gruppo = $daFirmare[$i..([Math]::Min($i + 39, $daFirmare.Count - 1))]
        & $signtool @argomentiFirma @gruppo | Out-Host
        if ($LASTEXITCODE -ne 0) { throw "Firma dei file del programma non riuscita." }
    }
}

if ($Fase -eq 'Pubblica') {
    Write-Host "== Fatto: programma pubblicato in $pubblicazione" -ForegroundColor Green
    return
}

# ---- Installer ----

Write-Host "== Creo l'installer con Inno Setup" -ForegroundColor Cyan
$argomentiInno = @('/Q', "/DVersione=$Versione", "/DPubblicazione=$pubblicazione")
if ($signtool) {
    # Inno Setup chiama signtool per l'installer e per il programma di disinstallazione.
    # Nel comando, $q sta per le virgolette e $f per il file da firmare.
    $parti = foreach ($a in $argomentiFirma) { if ($a -match '\s') { '$q' + $a + '$q' } else { $a } }
    $comando = '$q' + $signtool + '$q ' + ($parti -join ' ') + ' $f'
    $argomentiInno += @('/DFirma', "/Sfirma=$comando")
}
& $iscc @argomentiInno (Join-Path $PSScriptRoot 'TraduttoreLive.iss')
if ($LASTEXITCODE -ne 0) { throw "Inno Setup non riuscito." }

$installer = Join-Path $uscita "TraduttoreLive-Setup-$Versione.exe"
$mb = [Math]::Round((Get-Item $installer).Length / 1MB, 1)
if ($signtool) {
    $firma = Get-AuthenticodeSignature $installer
    if ($firma.Status -ne 'Valid') { throw "L'installer non risulta firmato correttamente: $($firma.StatusMessage)" }
    Write-Host "== Fatto: $installer ($mb MB), firmato da $($firma.SignerCertificate.Subject)" -ForegroundColor Green
}
else {
    Write-Host "== Fatto: $installer ($mb MB)" -ForegroundColor Green
    Write-Warning "Installer NON firmato dallo script (con GitHub Actions lo firma SignPath, se configurato; in locale usa -Certificato o -ArtifactSigning)."
}
