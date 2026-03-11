#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/.." && pwd)"
PROJECT_PATH="${REPO_ROOT}/src/PulseGadget.Cli/PulseGadget.Cli.csproj"

RID=""
CONFIGURATION="Release"
SELF_CONTAINED="true"

print_help() {
  cat <<'USAGE'
Publish PulseGadget CLI as a single-file artifact.

Usage:
  ./scripts/publish-single-cli.sh --rid <runtime-id> [--configuration Release|Debug] [--no-self-contained]

Examples:
  ./scripts/publish-single-cli.sh --rid linux-x64
  ./scripts/publish-single-cli.sh --rid win-x64 --configuration Release
  ./scripts/publish-single-cli.sh --rid osx-arm64 --no-self-contained
USAGE
}

while [[ $# -gt 0 ]]; do
  case "$1" in
    --rid)
      RID="${2:-}"
      shift 2
      ;;
    --configuration)
      CONFIGURATION="${2:-}"
      shift 2
      ;;
    --no-self-contained)
      SELF_CONTAINED="false"
      shift
      ;;
    -h|--help)
      print_help
      exit 0
      ;;
    *)
      echo "Unknown argument: $1" >&2
      print_help
      exit 1
      ;;
  esac
done

if [[ -z "${RID}" ]]; then
  echo "Error: --rid is required (e.g., linux-x64, win-x64, osx-arm64)." >&2
  print_help
  exit 1
fi

if ! command -v dotnet >/dev/null 2>&1; then
  echo "Error: dotnet CLI is not installed or not in PATH." >&2
  exit 1
fi

OUT_DIR="${REPO_ROOT}/artifacts/cli/${RID}"
mkdir -p "${OUT_DIR}"

echo "Publishing single-file CLI artifact..."
echo "  Project: ${PROJECT_PATH}"
echo "  RID: ${RID}"
echo "  Configuration: ${CONFIGURATION}"
echo "  Self-contained: ${SELF_CONTAINED}"
echo "  Output: ${OUT_DIR}"

dotnet publish "${PROJECT_PATH}" \
  -c "${CONFIGURATION}" \
  -r "${RID}" \
  --self-contained "${SELF_CONTAINED}" \
  -o "${OUT_DIR}" \
  /p:PublishSingleFile=true \
  /p:IncludeNativeLibrariesForSelfExtract=true \
  /p:DebugSymbols=false \
  /p:DebugType=None

if [[ "${RID}" == win-* ]]; then
  ARTIFACT_SOURCE="${OUT_DIR}/PulseGadget.Cli.exe"
  ARTIFACT_TARGET="${OUT_DIR}/pulse-gadget.exe"
else
  ARTIFACT_SOURCE="${OUT_DIR}/PulseGadget.Cli"
  ARTIFACT_TARGET="${OUT_DIR}/pulse-gadget"
fi

if [[ -f "${ARTIFACT_SOURCE}" ]]; then
  cp -f "${ARTIFACT_SOURCE}" "${ARTIFACT_TARGET}"
  chmod +x "${ARTIFACT_TARGET}" || true
  echo "Created artifact: ${ARTIFACT_TARGET}"
else
  echo "Warning: expected published binary not found at ${ARTIFACT_SOURCE}" >&2
fi
