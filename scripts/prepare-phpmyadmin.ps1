$ErrorActionPreference = 'Stop'
$publish = Join-Path $PSScriptRoot '..\publish'
$tools = Join-Path $publish 'tools'
$target = Join-Path $tools 'phpmyadmin'
$temp = Join-Path $env:RUNNER_TEMP 'phpmyadmin-5.2.3.zip'
$url = 'https://files.phpmyadmin.net/phpMyAdmin/5.2.3/phpMyAdmin-5.2.3-all-languages.zip'
New-Item -ItemType Directory -Force -Path $tools | Out-Null
Write-Host 'Downloading phpMyAdmin 5.2.3...'
& curl.exe -L --fail --silent --show-error -A 'Mozilla/5.0 STAVCMS-Build/0.7' -o $temp $url
$extract = Join-Path $env:RUNNER_TEMP 'pma-extract'
Remove-Item $extract -Recurse -Force -ErrorAction SilentlyContinue
Expand-Archive -Path $temp -DestinationPath $extract -Force
Remove-Item $target -Recurse -Force -ErrorAction SilentlyContinue
Move-Item (Join-Path $extract 'phpMyAdmin-5.2.3-all-languages') $target
$config = @'
<?php
$cfg['blowfish_secret'] = 'stavcms-local-server-portable-2026';
$i = 1;
$cfg['Servers'][$i]['auth_type'] = 'cookie';
$cfg['Servers'][$i]['host'] = '127.0.0.1';
$cfg['Servers'][$i]['compress'] = false;
$cfg['Servers'][$i]['AllowNoPassword'] = true;
'@
Set-Content -Path (Join-Path $target 'config.inc.php') -Value $config -Encoding UTF8
Write-Host 'phpMyAdmin prepared.'
