#!/bin/bash

# Estrae la prima chiave da ApiKeySettings__Keys (formato: "key1,key2,...")
API_KEY=$(echo "$ApiKeySettings__Keys" | cut -d',' -f1)

# Genera config.js con la configurazione runtime
cat > /app/wwwroot/admin/config.js << EOF
window.APP_CONFIG = {
  apiKey: "${API_KEY:-default-key-change-me}"
};
EOF

# Avvia l'applicazione .NET
exec dotnet TIRConnector.API.dll
