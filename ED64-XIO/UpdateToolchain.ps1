New-Item -ItemType Directory -Force -Path "$PSScriptRoot\temp\"

$url = "https://n64tools.blob.core.windows.net/binaries/N64-tools/libdragon/develop/latest/libdragon-win64.zip"
$output = "$PSScriptRoot\temp\libdragon.zip"

Invoke-WebRequest -Uri $url -OutFile $output
Expand-Archive -Force -Path $output -DestinationPath "$PSScriptRoot\toolchain\libdragon\"

$url = "https://n64tools.blob.core.windows.net/binaries/N64-tools/mips64-gcc-toolchain/master/latest/gcc-toolchain-mips64-win64.zip"
$output = "$PSScriptRoot\temp\gcc-toolchain-mips64.zip"

Invoke-WebRequest -Uri $url -OutFile $output
Expand-Archive -Force -Path $output -DestinationPath "$PSScriptRoot\toolchain\gcc-toolchain-mips64\"

Remove-Item -LiteralPath "$PSScriptRoot\temp\" -Force -Recurse