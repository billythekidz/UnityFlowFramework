﻿# ObjectsBinding Source Generator - Build Instructions

## ⚠️ CURRENTLY DISABLED

**Roslyn Source Generator is disabled due to Unity 6 compatibility issues.**

The framework now uses **Editor-time code generation** instead:
- Active generator: `Assets/.../ObjectsBinding/Editor/FallbackCodeGenerator.cs`
- DLL status: Renamed to `.disabled` to prevent Unity from loading it

---

This folder contains the Roslyn Source Generator DLL and build tools (for future use or older Unity versions).

**For framework usage guide, see:** `../README.md`

---

## 📁 Structure

```
SourceGenerator/
├── BuildSourceGenerator.ps1                # ⭐ BUILD SCRIPT
├── ObjectsBinding.SourceGenerator.dll      # ⭐ Generator DLL (RoslynAnalyzer)
├── README.md                               # This file (build guide)
└── BuildDLL/                               # Source code và build files
    ├── ObjectBindingGenerator.cs.bak       # Source code của generator
    ├── ObjectsBinding.SourceGenerator.Standalone.csproj  # Project file
    └── Output/                             # Build output (ignored in git)
```

---

## 🚀 Building the Generator

### Prerequisites:

- **.NET SDK** (6.0 or later)
- **PowerShell**

### Build Command:

#### From SourceGenerator folder:

```powershell
.\BuildSourceGenerator.ps1
```

#### From workspace root:

```powershell
.\Assets\LEARNING\GameFlowFramework\ObjectsBinding\SourceGenerator\BuildSourceGenerator.ps1
```

---

## 🔄 Build Process

The script automatically:

```
[1/5] Restore source files from .bak
      ObjectBindingGenerator.cs.bak → ObjectBindingGenerator.cs

[2/5] Build DLL with dotnet
      dotnet build → Output/ObjectsBinding.SourceGenerator.Standalone.dll

[3/5] Copy DLL to SourceGenerator folder
      Output/*.dll → ObjectsBinding.SourceGenerator.dll

[4/5] Backup source files again
      ObjectBindingGenerator.cs → ObjectBindingGenerator.cs.bak

[5/5] Clean up build artifacts
      Remove temporary files from Output/
```

---

## 📊 Build Output

```
Build succeeded!
✓ DLL copied to: ObjectsBinding.SourceGenerator.dll
  Size: 4.5 KB
✓ Source generator built successfully!
```

---

## ⚙️ Configuration

### Project File:

**Location:** `BuildDLL/ObjectsBinding.SourceGenerator.Standalone.csproj`

```xml
<PropertyGroup>
  <TargetFramework>netstandard2.0</TargetFramework>
  <IsRoslynComponent>true</IsRoslynComponent>
  <OutputPath>Output\</OutputPath>
</PropertyGroup>

<ItemGroup>
  <PackageReference Include="Microsoft.CodeAnalysis.CSharp" Version="4.0.1" PrivateAssets="all" />
  <PackageReference Include="System.Text.Json" Version="8.0.0" PrivateAssets="all" />
</ItemGroup>
```

**Note:** `PrivateAssets="all"` embeds dependencies into the DLL.

---

## 🔍 Source Code

### Generator Source:

**File:** `BuildDLL/ObjectBindingGenerator.cs.bak`

This is the source code for the Roslyn Source Generator.

**Why `.bak` extension?**
- Prevents Unity from compiling it as a regular script
- Only used during build process
- Automatically restored/backed up by build script

### To Modify:

1. **Edit:** `BuildDLL/ObjectBindingGenerator.cs.bak`
2. **Build:** `.\BuildSourceGenerator.ps1`
3. **Restart Unity** to activate new generator

---

## 🏷️ DLL Configuration

### RoslynAnalyzer Label:

**File:** `ObjectsBinding.SourceGenerator.dll.meta`

```yaml
labels:
- RoslynAnalyzer  # ← REQUIRED!
```

**Important:** Without this label, Unity won't recognize the DLL as a source generator!

### To Verify:

1. Select `ObjectsBinding.SourceGenerator.dll` in Unity Project window
2. Inspector → Plugin settings → Labels
3. Must show: `RoslynAnalyzer`

---

## 🛠️ Development Workflow

### 1. Modify Generator:

```bash
# Edit source code
code BuildDLL/ObjectBindingGenerator.cs.bak
```

### 2. Build:

```powershell
.\BuildSourceGenerator.ps1
```

### 3. Test in Unity:

```
1. Restart Unity Editor (REQUIRED!)
2. Create/modify class with [GenerateObjectBinding]
3. Check generated code
4. Review Console for errors
```

### 4. Iterate:

If changes needed, repeat steps 1-3.

---

## 🧹 Clean Build

### Manual Clean:

```powershell
Remove-Item BuildDLL/obj -Recurse -Force
Remove-Item BuildDLL/Output -Recurse -Force
```

### Rebuild:

```powershell
.\BuildSourceGenerator.ps1
```

---

## 📦 Git Configuration

### Ignored Files:

**.gitignore:**
```gitignore
BuildDLL/obj/
BuildDLL/Output/
BuildDLL/bin/
*.deps.json
*.pdb
```

### Committed Files:

- ✅ `ObjectsBinding.SourceGenerator.dll` (output)
- ✅ `BuildSourceGenerator.ps1` (build script)
- ✅ `BuildDLL/ObjectBindingGenerator.cs.bak` (source)
- ✅ `BuildDLL/*.csproj` (project file)

---

## 🔧 Troubleshooting

### Build Fails:

#### 1. Check .NET SDK:

```powershell
dotnet --version
# Should show: 6.0.0 or later
```

#### 2. Check Project File:

```powershell
dotnet build BuildDLL/ObjectsBinding.SourceGenerator.Standalone.csproj -c Release
# Check for specific errors
```

#### 3. Clean and Rebuild:

```powershell
Remove-Item BuildDLL/obj -Recurse -Force
.\BuildSourceGenerator.ps1
```

### Generator Not Working in Unity:

#### 1. **Restart Unity!**
Most important! Generator only loads on startup.

#### 2. Check DLL Label:
Must have `RoslynAnalyzer` in meta file.

#### 3. Check Console:
Look for source generator errors.

#### 4. Verify DLL Exists:
```powershell
Test-Path ObjectsBinding.SourceGenerator.dll
# Should return: True
```

---

## 📝 Notes

### Dependencies:

All dependencies are embedded in the DLL:
- `Microsoft.CodeAnalysis.CSharp`
- `Microsoft.CodeAnalysis.Analyzers`
- `System.Text.Json`

No need to ship separate dependency DLLs!

### Build Artifacts:

Temporary files in `BuildDLL/Output/` and `BuildDLL/obj/` are:
- Generated during build
- Ignored by git
- Can be safely deleted

### Source Backup:

The build script manages `.bak` files automatically:
- Restores before build
- Backs up after build
- Prevents Unity compilation conflicts

---

## ✅ Build Checklist

Before committing generator changes:

- [ ] Build succeeds without errors
- [ ] DLL file size is reasonable (~4-5 KB)
- [ ] RoslynAnalyzer label is set
- [ ] Tested in Unity after restart
- [ ] Generated code works as expected
- [ ] No console errors
- [ ] Source backed up to `.bak`

---

## 🆘 Support

### Common Issues:

**"dotnet command not found"**
→ Install .NET SDK from: https://dotnet.microsoft.com/download

**"Project file could not be loaded"**
→ Check `.csproj` file for XML errors

**"Build succeeded but Unity doesn't generate code"**
→ Restart Unity Editor!

**"DLL size is 0 bytes"**
→ Build failed silently, check build output

---

## 📚 See Also

- **Framework Usage:** `../README.md`
- **Generator Source:** `BuildDLL/ObjectBindingGenerator.cs.bak`
- **Project File:** `BuildDLL/ObjectsBinding.SourceGenerator.Standalone.csproj`

---

**For generator usage and troubleshooting, see the main README:** `../README.md`

**This file focuses on building the generator DLL only.** 🔨
