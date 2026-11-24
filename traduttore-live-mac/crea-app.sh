#!/bin/bash
# Crea «Traduttore dal vivo.app» nella cartella out/, pronta da copiare in Applicazioni.
#
#   ./crea-app.sh            per il Mac su cui lo esegui (Apple Silicon o Intel)
#   ./crea-app.sh arm64      per i Mac con Apple Silicon
#   ./crea-app.sh x86_64     per i Mac con processore Intel
#
# Servono il .NET SDK 10 (o successivo) e gli strumenti per sviluppatori di Apple (xcode-select --install).
# L'app include il runtime .NET: sul Mac dove la usi non serve installare altro. È firmata solo localmente
# («ad hoc»): sugli altri Mac, la prima volta, va aperta con clic destro › Apri.

set -euo pipefail
cd "$(dirname "$0")"

ARCHITETTURA="${1:-$(uname -m)}"
case "$ARCHITETTURA" in
    arm64) RID=osx-arm64; LIBRERIE=macos-arm64 ;;
    x86_64 | x64 | intel) RID=osx-x64; LIBRERIE=macos-x64 ;;
    *) echo "Architettura sconosciuta: $ARCHITETTURA (usa arm64 o x86_64)" >&2; exit 1 ;;
esac

VERSIONE=$(sed -n 's:.*<Version>\(.*\)</Version>.*:\1:p' TraduttoreLiveMac.csproj | head -1)
PUBBLICAZIONE="out/publish-$RID"
APP="out/Traduttore dal vivo.app"

echo "▸ Compilo Traduttore dal vivo $VERSIONE per ${RID}…"
rm -rf "$PUBBLICAZIONE" "$APP"
dotnet publish TraduttoreLiveMac.csproj -c Release -r "$RID" --self-contained true -o "$PUBBLICAZIONE" -nologo -v quiet

echo "▸ Creo il pacchetto dell'app…"
mkdir -p "$APP/Contents/MacOS" "$APP/Contents/Resources"
cp -R "$PUBBLICAZIONE/." "$APP/Contents/MacOS/"
rm -f "$APP/Contents/MacOS/"*.pdb
# Delle librerie native (Whisper: macos-*, AI integrata: osx-*) servono solo quelle di questo Mac.
find "$APP/Contents/MacOS/runtimes" -mindepth 1 -maxdepth 1 -type d ! -name "$LIBRERIE" ! -name "$RID" -exec rm -rf {} +
sed "s/__VERSIONE__/$VERSIONE/g" Info.plist > "$APP/Contents/Info.plist"
printf 'APPL????' > "$APP/Contents/PkgInfo"

echo "▸ Preparo l'icona…"
ICONSET="$(mktemp -d)/icona.iconset"
mkdir -p "$ICONSET"
BASE="$(dirname "$ICONSET")/icona.png"
sips -s format png ../traduttore-live/icona.ico --out "$BASE" >/dev/null
for LATO in 16 32 128 256 512; do
    for SCALA in 1 2; do
        PIXEL=$((LATO * SCALA))
        NOME="icon_${LATO}x${LATO}"
        [ "$SCALA" = 2 ] && NOME="${NOME}@2x"
        # Come le icone del Mac: il disegno occupa circa l'80% del quadrato.
        INTERNO=$((PIXEL * 80 / 100))
        sips -z "$INTERNO" "$INTERNO" "$BASE" --out "$ICONSET/tmp.png" >/dev/null
        sips -p "$PIXEL" "$PIXEL" "$ICONSET/tmp.png" --out "$ICONSET/$NOME.png" >/dev/null
    done
done
rm -f "$ICONSET/tmp.png"
iconutil -c icns "$ICONSET" -o "$APP/Contents/Resources/icona.icns"
rm -rf "$(dirname "$ICONSET")"

echo "▸ Firmo l'app (firma locale)…"
# macOS considera codice tutto quello che sta in Contents/MacOS, anche le librerie .NET: si firmano una per una
# (per i file che non sono programmi del Mac la firma resta negli attributi estesi), poi il pacchetto.
find "$APP/Contents/MacOS" -type f ! -path "*/Contents/MacOS/TraduttoreLive" -print0 \
    | xargs -0 codesign --force --sign - 2>/dev/null
codesign --force --sign - "$APP" 2>/dev/null
codesign --verify --deep --strict "$APP"

DIMENSIONE=$(du -sh "$APP" | cut -f1)
echo
echo "✓ Creata: $(pwd)/$APP ($DIMENSIONE)"
echo "  Per installarla trascinala in Applicazioni, oppure: cp -R \"$APP\" /Applications/"
