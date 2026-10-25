$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$project = Join-Path $root 'src\PolarRecorder\PolarRecorder.csproj'
$out = Join-Path $root 'dist\PolarRec'
$bundle = Join-Path $root 'app\tools'
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) { throw 'Install .NET 8 SDK first.' }
# For source-only checkouts, acquire the reviewed archive. Updates must be reviewed and re-pinned.
if (-not (Test-Path (Join-Path $bundle 'ffmpeg.exe'))) {
    $temp = Join-Path ([IO.Path]::GetTempPath()) ([Guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory $temp | Out-Null
    try {
        $archive = Join-Path $temp 'engine.zip'
        Invoke-WebRequest 'https://www.gyan.dev/ffmpeg/builds/ffmpeg-release-essentials.zip' -OutFile $archive
        $expected = '60f467265b1e312373dbcd92200c2618a74850f98d3d078e94296bb3fa2047ba'
        if ((Get-FileHash $archive -Algorithm SHA256).Hash.ToLowerInvariant() -ne $expected) { throw 'Upstream archive changed. Review and pin the new build before packaging.' }
        Expand-Archive $archive (Join-Path $temp 'extracted')
        $package = Join-Path $temp 'extracted\ffmpeg-9.0.2-essentials_build'
        New-Item -ItemType Directory -Force $bundle | Out-Null
        Copy-Item (Join-Path $package 'bin\ffmpeg.exe') $bundle
        Copy-Item (Join-Path $package 'LICENSE') $bundle
        Copy-Item (Join-Path $package 'README.txt') $bundle
        Set-Content (Join-Path $bundle 'archive-sha256.txt') "$expected  ffmpeg-release-essentials.zip"
    } finally { Remove-Item -LiteralPath $temp -Recurse -Force }
}
dotnet publish $project -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -o $out
if ($LASTEXITCODE -ne 0) { throw 'Publish failed.' }
New-Item -ItemType Directory -Force (Join-Path $out 'tools') | Out-Null
Copy-Item (Join-Path $bundle '*') (Join-Path $out 'tools') -Recurse -Force
Copy-Item (Join-Path $root 'README.md') $out
Copy-Item (Join-Path $root 'licenses') $out -Recurse -Force
Write-Host "Ready: $out\PolarRec.exe (engine included)"
