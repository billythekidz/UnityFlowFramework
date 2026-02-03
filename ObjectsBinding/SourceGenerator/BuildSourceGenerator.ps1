﻿# Build script for ObjectsBinding Source Generator
# This script compiles the source generator into a DLL that Unity can use

$ErrorActionPreference = "Stop"

# Get script location
$scriptPath = Split-Path -Parent $MyInvocation.MyCommand.Path

# Use relative paths from script location
$buildDllPath = Join-Path $scriptPath "BuildDLL"
$sourceGeneratorPath = $scriptPath
$projectFile = Join-Path $buildDllPath "ObjectsBinding.SourceGenerator.Standalone.csproj"
$outputPath = $sourceGeneratorPath  # Output directly to SourceGenerator folder
$csFile = Join-Path $buildDllPath "ObjectBindingGenerator.cs"
$csBackup = Join-Path $buildDllPath "ObjectBindingGenerator.cs.bak"
$metaFile = Join-Path $buildDllPath "ObjectBindingGenerator.cs.meta"
$metaBackup = Join-Path $buildDllPath "ObjectBindingGenerator.cs.meta.bak"

Write-Host "Building ObjectsBinding Source Generator..." -ForegroundColor Cyan

# Restore .cs file if it's backed up
$needsBackup = $false
if (Test-Path $csBackup) {
    Write-Host "Restoring ObjectBindingGenerator.cs from backup..." -ForegroundColor Yellow
    Move-Item -Force $csBackup $csFile
    if (Test-Path $metaBackup) {
        Move-Item -Force $metaBackup $metaFile
    }
    $needsBackup = $true
}

# Build the project
Write-Host "Compiling source generator..." -ForegroundColor Cyan
dotnet build $projectFile -c Release

if ($LASTEXITCODE -eq 0) {
    Write-Host "Build successful!" -ForegroundColor Green
    
    # Copy and rename the output DLL from BuildDLL/Output to SourceGenerator
    $standaloneDll = Join-Path $buildDllPath "Output\ObjectsBinding.SourceGenerator.Standalone.dll"
    $targetDll = Join-Path $outputPath "ObjectsBinding.SourceGenerator.dll"
    
    if (Test-Path $standaloneDll) {
        Copy-Item -Force $standaloneDll $targetDll
        Write-Host "✓ DLL copied to: $targetDll" -ForegroundColor Green
        
        $dllSize = (Get-Item $targetDll).Length
        $dllSizeKB = [math]::Round($dllSize / 1KB, 2)
        Write-Host "  Size: $dllSizeKB KB" -ForegroundColor Gray
        
        # Setup .meta file for Editor-only with RoslynAnalyzer label
        $dllMetaFile = "$targetDll.meta"
        Write-Host "Setting up .meta file..." -ForegroundColor Cyan
        
        # Generate GUID for the DLL (use existing if meta exists)
        $guid = "c6c7352d990ce8d4e9559c401b0f50f4"  # Use consistent GUID
        if (Test-Path $dllMetaFile) {
            # Try to preserve existing GUID
            $existingContent = Get-Content $dllMetaFile -Raw
            if ($existingContent -match "guid: ([a-f0-9]+)") {
                $guid = $matches[1]
                Write-Host "  Preserving existing GUID: $guid" -ForegroundColor Gray
            }
        }
        
        # Create .meta content with Editor-only configuration (Unity format)
        $metaContent = @"
fileFormatVersion: 2
guid: $guid
labels:
- RoslynAnalyzer
PluginImporter:
  externalObjects: {}
  serializedVersion: 3
  iconMap: {}
  executionOrder: {}
  defineConstraints: []
  isPreloaded: 0
  isOverridable: 0
  isExplicitlyReferenced: 0
  validateReferences: 1
  platformData:
    Any:
      enabled: 0
      settings:
        Exclude Editor: 0
        Exclude Linux64: 1
        Exclude OSXUniversal: 1
        Exclude WebGL: 1
        Exclude Win: 1
        Exclude Win64: 1
    Editor:
      enabled: 1
      settings:
        CPU: AnyCPU
        DefaultValueInitialized: true
        OS: AnyOS
    Linux64:
      enabled: 0
      settings:
        CPU: AnyCPU
    OSXUniversal:
      enabled: 0
      settings:
        CPU: AnyCPU
    Win:
      enabled: 0
      settings:
        CPU: AnyCPU
    Win64:
      enabled: 0
      settings:
        CPU: AnyCPU
    WindowsStoreApps:
      enabled: 0
      settings:
        CPU: AnyCPU
  userData: 
  assetBundleName: 
  assetBundleVariant: 
"@
        
        Set-Content -Path $dllMetaFile -Value $metaContent -Encoding UTF8 -NoNewline
        Write-Host "✓ .meta file configured (Editor-only, RoslynAnalyzer)" -ForegroundColor Green
    }
    else {
        Write-Host "⚠ Warning: Built DLL not found at $standaloneDll" -ForegroundColor Yellow
    }
    
    # Backup the .cs file again if we restored it
    if ($needsBackup) {
        Write-Host "Backing up ObjectBindingGenerator.cs..." -ForegroundColor Yellow
        Move-Item -Force $csFile $csBackup
        if (Test-Path $metaFile) {
            Move-Item -Force $metaFile $metaBackup
        }
    }
    
    Write-Host "`n✓ Source generator built successfully!" -ForegroundColor Green
    Write-Host "✓ Please restart Unity for changes to take effect." -ForegroundColor Yellow
} else {
    Write-Host "Build failed!" -ForegroundColor Red
    
    # Restore backup if build failed
    if ($needsBackup -and (Test-Path $csFile)) {
        Write-Host "Restoring backup due to build failure..." -ForegroundColor Yellow
        Move-Item -Force $csFile $csBackup
        if (Test-Path $metaFile) {
            Move-Item -Force $metaFile $metaBackup
        }
    }
    
    exit 1
}

