
# Luafuscator Deobfuscator

C# Deobfuscator for Luafuscator 1.0.8+

## Usage

```bash
1.0.8.0-D.exe "obfuscated.lua"
```

**Recommended:**
```bash
1.0.8.0-D.exe input.lua --verbose
```

### Options

| Option        | Shortcut | Description                                   |
|---------------|----------|-----------------------------------------------|
| `--analyze`   | `-a`     | Show analysis + executor warnings             |
| `--verbose`   | `-v`     | Show detailed output                          |
| `--quiet`     | `-q`     | Suppress string output                        |
| `--printable` | `-p`     | Show only printable strings                   |
| `--no-lfr`    |          | Skip _LFR resolution                          |
| `--no-fold`   |          | Skip constant folding                         |
| `--no-ast`    |          | Skip AST renaming pass                        |

Output is saved as **`deobfuscated.lua`**.

---

## Build Instructions

1. Open the solutionin **Visual Studio**
2. Set configuration to **Release**
3. Set platform to **Any CPU** (recommended) or **x64**
4. Target **.NET 8.0** (or newer)
5. Build the project

The executable will be in:  
`bin\Release\net8.0\1.0.8.0-D.exe`
If you use NET 10.0 then it would be `bin\Release\net10.0\1.0.8.0-D.exe`

---

## ⚠️ Disclaimer

This tool is for **educational and analysis purposes** only.  
Always review the deobfuscated script before running it.

---

## Author

**Created and maintained by vyxonq**
- Discord: `1227908670394863639` (for contact or help)

---

**Licensed under the MIT License**

```