# Bai-Pass

Gestor de contraseñas de escritorio para **Windows y macOS** que almacena tus credenciales de forma cifrada.
Construido con **C# / .NET 10** y **Avalonia UI** (una sola base de código para ambos sistemas).

## Seguridad

- Toda la bóveda se cifra con **AES-256-GCM** (cifrado autenticado: detecta cualquier manipulación).
- La clave se deriva de tu **contraseña maestra** con **PBKDF2-SHA256, 600 000 iteraciones** y sal aleatoria.
- La contraseña maestra **nunca se guarda**. Sin ella, el archivo es ilegible — no hay puerta trasera ni recuperación.
- El archivo cifrado vive en la carpeta de datos del usuario:
  - Windows: `%APPDATA%\PasswordManager\vault.dat`
  - macOS: `~/.config/PasswordManager/vault.dat`
- Al copiar una contraseña, el portapapeles se **limpia automáticamente** a los 25 segundos.
- **Auto-cierre de sesión a las 8 horas**: por seguridad, la sesión se bloquea sola y vuelve a la pantalla de login.

## Funciones

- Crear bóveda con contraseña maestra / desbloquear la existente.
- Agregar, editar y eliminar entradas (título, usuario, contraseña, URL, notas, carpeta).
- **Carpetas colapsables**: agrupa las credenciales por carpeta (p. ej. "QA", "Producción"); crea carpetas escribiendo su nombre y contrae/expande cada grupo.
- **Eliminación con doble confirmación** para evitar borrados accidentales.
- Búsqueda instantánea por título, usuario, URL o carpeta.
- Generador de contraseñas fuertes (20 caracteres, RNG criptográfico).
- Mostrar/ocultar y copiar usuario o contraseña con un clic.
- Guardado cifrado automático en cada cambio.

## Requisitos

- [.NET 10 SDK](https://dotnet.microsoft.com/download)

## Ejecutar en desarrollo

```bash
dotnet run
```

## Generar ejecutable independiente (no requiere .NET instalado en la máquina destino)

**Windows (x64):**
```bash
dotnet publish -c Release -r win-x64 --self-contained -p:PublishSingleFile=true
```

**macOS Apple Silicon (M1/M2/M3/M4):**
```bash
dotnet publish -c Release -r osx-arm64 --self-contained -p:PublishSingleFile=true
```

**macOS Intel:**
```bash
dotnet publish -c Release -r osx-x64 --self-contained -p:PublishSingleFile=true
```

El binario queda en `bin/Release/net10.0/<runtime>/publish/`.

## Instalador de Windows

El instalador (Inno Setup) se genera desde `installer/PasswordManager.iss`. Instala por-usuario
(sin admin), crea acceso directo y aparece en "Agregar o quitar programas".

## Estructura

```
Models/PasswordEntry.cs        Modelo de una credencial (incluye carpeta)
Services/CryptoService.cs      Cifrado AES-GCM + derivación PBKDF2
Services/VaultService.cs       Carga/guarda el archivo cifrado
Services/PasswordGenerator.cs  Generador de contraseñas y medidor de fuerza
Services/ClipboardService.cs   Portapapeles con auto-borrado
ViewModels/                    Lógica (MVVM): Unlock, Vault, FolderGroup, shell
Views/                         Interfaz (Avalonia XAML)
installer/                     Script del instalador (Inno Setup)
```
