$ErrorActionPreference = 'Stop'

$root = Resolve-Path (Join-Path $PSScriptRoot '..')
$publish = Join-Path $root 'publish'
$temp = Join-Path $root '.component-cache'
New-Item -ItemType Directory -Force -Path $temp | Out-Null
New-Item -ItemType Directory -Force -Path (Join-Path $publish 'bin') | Out-Null

function Download-File([string]$Url, [string]$OutFile) {
    Write-Host "Downloading $Url"
    Invoke-WebRequest -Uri $Url -OutFile $OutFile -UseBasicParsing -AllowInsecureRedirect
}

function Reset-Directory([string]$Path) {
    if (Test-Path $Path) { Remove-Item $Path -Recurse -Force }
    New-Item -ItemType Directory -Force -Path $Path | Out-Null
}

# Apache 2.4.66 Win64 VS17
$apacheZip = Join-Path $temp 'apache.zip'
Download-File 'https://www.apachelounge.com/download/VS17/binaries/httpd-2.4.66-251206-win64-VS17.zip' $apacheZip
$apacheExtract = Join-Path $temp 'apache'
Reset-Directory $apacheExtract
Expand-Archive $apacheZip -DestinationPath $apacheExtract -Force
$apacheSource = Join-Path $apacheExtract 'Apache24'
$apacheTarget = Join-Path $publish 'bin/apache'
if (Test-Path $apacheTarget) { Remove-Item $apacheTarget -Recurse -Force }
Move-Item $apacheSource $apacheTarget

# PHP 8.4 latest Win64 VS17 Thread Safe
$phpZip = Join-Path $temp 'php84.zip'
Download-File 'https://downloads.php.net/~windows/releases/latest/php-8.4-Win32-vs17-x64-latest.zip' $phpZip
$phpTarget = Join-Path $publish 'bin/php/8.4'
Reset-Directory $phpTarget
Expand-Archive $phpZip -DestinationPath $phpTarget -Force
Copy-Item (Join-Path $phpTarget 'php.ini-development') (Join-Path $phpTarget 'php.ini') -Force

# MariaDB 11.4.13 LTS Win64 ZIP
$mariaZip = Join-Path $temp 'mariadb.zip'
Download-File 'https://dlm.mariadb.com/4801422/MariaDB/mariadb-11.4.13/winx64-packages/mariadb-11.4.13-winx64.zip' $mariaZip
$mariaExtract = Join-Path $temp 'mariadb'
Reset-Directory $mariaExtract
Expand-Archive $mariaZip -DestinationPath $mariaExtract -Force
$mariaSource = Get-ChildItem $mariaExtract -Directory | Select-Object -First 1
$mariaTarget = Join-Path $publish 'bin/mariadb'
if (Test-Path $mariaTarget) { Remove-Item $mariaTarget -Recurse -Force }
Move-Item $mariaSource.FullName $mariaTarget

# Apache portable configuration with PHP module enabled.
$httpdConf = @'
Define SRVROOT ".."
ServerRoot "${SRVROOT}"
Listen 80
ServerName localhost:80
LoadModule access_compat_module modules/mod_access_compat.so
LoadModule actions_module modules/mod_actions.so
LoadModule alias_module modules/mod_alias.so
LoadModule auth_basic_module modules/mod_auth_basic.so
LoadModule authn_core_module modules/mod_authn_core.so
LoadModule authn_file_module modules/mod_authn_file.so
LoadModule authz_core_module modules/mod_authz_core.so
LoadModule authz_host_module modules/mod_authz_host.so
LoadModule authz_user_module modules/mod_authz_user.so
LoadModule dir_module modules/mod_dir.so
LoadModule env_module modules/mod_env.so
LoadModule headers_module modules/mod_headers.so
LoadModule log_config_module modules/mod_log_config.so
LoadModule mime_module modules/mod_mime.so
LoadModule rewrite_module modules/mod_rewrite.so
LoadModule setenvif_module modules/mod_setenvif.so
LoadModule php_module "../../php/8.4/php8apache2_4.dll"
PHPIniDir "../../php/8.4"
DirectoryIndex index.php index.html
TypesConfig conf/mime.types
AddType application/x-httpd-php .php
DocumentRoot "../../projects"
<Directory "../../projects">
    Options Indexes FollowSymLinks
    AllowOverride All
    Require all granted
</Directory>
ErrorLog "../../logs/apache-error.log"
CustomLog "../../logs/apache-access.log" common
'@
Set-Content -Path (Join-Path $apacheTarget 'conf/httpd.conf') -Value $httpdConf -Encoding UTF8

# Enable common PHP extensions used by STAVCMS.
$phpIni = Join-Path $phpTarget 'php.ini'
$ini = Get-Content $phpIni -Raw
$ini = $ini -replace ';extension_dir = "ext"', 'extension_dir = "ext"'
foreach ($ext in @('curl','fileinfo','gd','intl','mbstring','mysqli','openssl','pdo_mysql','zip')) {
    $ini = $ini -replace ";extension=$ext", "extension=$ext"
}
$ini += "`r`ndate.timezone=UTC`r`n"
Set-Content -Path $phpIni -Value $ini -Encoding UTF8

$mariaIni = @'
[mysqld]
port=3306
basedir=.
datadir=../../databases/mariadb-data
character-set-server=utf8mb4
collation-server=utf8mb4_unicode_ci
skip-name-resolve

[client]
port=3306
default-character-set=utf8mb4
'@
Set-Content -Path (Join-Path $mariaTarget 'my.ini') -Value $mariaIni -Encoding UTF8

Write-Host 'Portable server components prepared successfully.'
