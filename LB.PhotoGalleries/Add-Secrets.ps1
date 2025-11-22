param(
    [Parameter(Mandatory=$true)]
    [string]$SourceFile
)

# Verify the file exists
if (-not (Test-Path $SourceFile)) {
    Write-Error "File not found: $SourceFile"
    exit 1
}

# Read and parse the JSON file
$json = Get-Content -Path $SourceFile -Raw | ConvertFrom-Json

# Function to flatten nested JSON into configuration keys
function Get-FlattenedKeys {
    param (
        [Parameter(Mandatory)]
        $Object,
        
        [string]$Prefix = ""
    )
    
    $Object.PSObject.Properties | ForEach-Object {
        $key = if ($Prefix) { "$($Prefix):$($_.Name)" } else { $_.Name }
        
        if ($_.Value -is [PSCustomObject]) {
            # Recurse for nested objects
            Get-FlattenedKeys -Object $_.Value -Prefix $key
        }
        elseif ($_.Value -is [Array]) {
            # Handle arrays with indexed keys
            for ($i = 0; $i -lt $_.Value.Count; $i++) {
                if ($_.Value[$i] -is [PSCustomObject]) {
                    Get-FlattenedKeys -Object $_.Value[$i] -Prefix "$($key):$i"
                } else {
                    [PSCustomObject]@{ Key = "$($key):$i"; Value = $_.Value[$i] }
                }
            }
        }
        else {
            # Output the flattened key-value pair
            [PSCustomObject]@{ Key = $key; Value = $_.Value }
        }
    }
}

# Flatten the JSON and apply to user-secrets
Get-FlattenedKeys -Object $json | ForEach-Object {
    dotnet user-secrets set $_.Key $_.Value
    Write-Host "Set: $($_.Key)" -ForegroundColor Green
}

Write-Host "`nSuccessfully imported secrets from $SourceFile" -ForegroundColor Cyan