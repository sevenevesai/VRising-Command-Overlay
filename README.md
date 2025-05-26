# VRising-Command-Overlay

**Visual command overlay for modded V Rising.**

An open-source, low-impact overlay for issuing mod commands in [V Rising](https://store.steampowered.com/app/1604030/V_Rising/) via a simple in-game UI.

---

### Features

- Written in C# — runs natively as a Windows `.exe`
- Clickable buttons that send chat commands to the game
- Supports prompts with text input or option picklists
- Drag-to-reposition overlay anywhere on screen
- Backed by a user-editable `commands.json`
- Dynamic: just edit the JSON and restart to change the UI

---

## Setup
You may always download the latest exe from the [Releases](https://github.com/sevenevesai/VRising-Command-Overlay/releases) page -- no install needed, just download and start it up.

Or, if you'd rather make/tweak/review the build yourself, follow the below:

1. **Clone the repo**

    ```bash
    git clone https://github.com/sevenevesai/VRising-Command-Overlay.git
    cd VRising-Command-Overlay
    ```

2. **Open the project**  
   Open `overlayc.csproj` in Visual Studio 2022+ or VS Code with the C# Dev Kit.

3. **Build or run**  
   Press `F5` or use:

    ```bash
    dotnet build
    ```

4. **Customize your commands**  
   Edit `commands.json` to change categories, commands, or prompts.  
   Changes take effect on restart.

---

## Optional: Build as standalone `.exe`

To generate a portable single-file executable:

```bash
dotnet publish -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true
