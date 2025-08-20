#!/bin/bash
# Bash script to generate ApiKeys.cs from template with actual API key
# This should be run before building the mod

set -e

API_KEY=""
TEMPLATE_FILE="../src/Config/ApiKeys.cs.template"
OUTPUT_FILE="../src/Config/ApiKeys.cs"

# Parse command line arguments
while [[ $# -gt 0 ]]; do
    case $1 in
        --api-key)
            API_KEY="$2"
            shift 2
            ;;
        --template)
            TEMPLATE_FILE="$2"
            shift 2
            ;;
        --output)
            OUTPUT_FILE="$2"
            shift 2
            ;;
        *)
            echo "Unknown option: $1"
            exit 1
            ;;
    esac
done

# Try to get API key from various sources
if [ -z "$API_KEY" ]; then
    # 1. Environment variable
    API_KEY="$FOLLOWME_PEAK_API_KEY"
fi

if [ -z "$API_KEY" ]; then
    # 2. Local config file (not in git)
    if [ -f "api-config.json" ]; then
        API_KEY=$(cat api-config.json | grep -o '"apiKey"[[:space:]]*:[[:space:]]*"[^"]*"' | sed 's/.*"apiKey"[[:space:]]*:[[:space:]]*"\([^"]*\)".*/\1/')
    fi
fi

if [ -z "$API_KEY" ]; then
    # 3. Prompt user
    echo -n "Enter API Key for FollowMe Peak: "
    read -s API_KEY
    echo
fi

if [ -z "$API_KEY" ] || [ ${#API_KEY} -lt 10 ]; then
    echo "❌ Invalid or missing API key. API key must be at least 10 characters long."
    exit 1
fi

# Check if template exists
if [ ! -f "$TEMPLATE_FILE" ]; then
    echo "❌ Template file not found: $TEMPLATE_FILE"
    exit 1
fi

# Read template and replace placeholder
content=$(cat "$TEMPLATE_FILE" | sed "s/{{API_KEY_PLACEHOLDER}}/$API_KEY/g")

# Ensure output directory exists
mkdir -p "$(dirname "$OUTPUT_FILE")"

# Write output file
echo "$content" > "$OUTPUT_FILE"

echo "✅ ApiKeys.cs generated successfully with API key"
echo "⚠️  WARNING: Do not commit the generated ApiKeys.cs file to git!"

# Optional: Create a local config file for future builds
echo -n "Save API key locally for future builds? (y/N): "
read save_config
if [ "$save_config" = "y" ] || [ "$save_config" = "Y" ]; then
    cat > api-config.json << EOF
{
    "apiKey": "$API_KEY",
    "generated": "$(date -Iseconds)"
}
EOF
    echo "✅ API key saved to api-config.json (excluded from git)"
fi