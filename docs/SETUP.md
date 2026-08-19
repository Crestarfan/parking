# Setup Guide

## Configure Local Secrets

Before running the application, you need to configure the required API keys locally using .NET user-secrets.

### For zsh/bash

```bash
dotnet user-secrets set "GoogleMaps:ApiKey" "<your-restricted-google-maps-key>" \
  --project src/CarparkAvailability.AppHost

dotnet user-secrets set "DataGovSg:ApiKey" "<your-data-gov-sg-key>" \
  --project src/CarparkAvailability.AppHost
```

### For PowerShell

```powershell
dotnet user-secrets set "GoogleMaps:ApiKey" "<your-restricted-google-maps-key>" `
  --project src/CarparkAvailability.AppHost

dotnet user-secrets set "DataGovSg:ApiKey" "<your-data-gov-sg-key>" `
  --project src/CarparkAvailability.AppHost
```

### Required Keys

- **GoogleMaps:ApiKey** - Your restricted Google Maps API key
- **DataGovSg:ApiKey** - Your Data.gov.sg API key

### Notes

- Replace `<your-restricted-google-maps-key>` with your actual Google Maps API key
- Replace `<your-data-gov-sg-key>` with your actual Data.gov.sg API key
- These secrets are stored locally and not committed to version control
- Each developer must configure these secrets on their machine
