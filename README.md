# 🐍 RetroSnake — Premium Arcade Terminal Console Game

### ✨ Reviving Retro Arcade Classics Through Modern .NET Architecture

**RetroSnake** isn't just a simple command-line script; it's a production-ready, high-performance console engine built on modern .NET principles. By utilizing specialized memory-buffered rendering techniques instead of disruptive terminal clearing, RetroSnake achieves a perfectly smooth, flicker-free arcade simulation straight inside your native system terminal.

Built entirely using **C#** and targeting the cutting-edge **.NET 10.0 runtime**, this architecture demonstrates how object-oriented game loops, non-blocking asynchronous input handlers, and localized double-buffer graphic loops can be orchestrated to run flawlessly inside a single console app.

---

## 🖼️ Visuals & Component Walkthrough

### 🕹️ Central Game Arena
The main game grid features a clean HUD system displaying live game telemetry, score tracking, and persistent high scores.
![RetroSnake Game Arena](media/fig_arena.png)

### 🟩 Component 1: Flicker-Free Dual-Buffer Renderer
An optimized memory grid engine that maintains a copy of the previous terminal frame. Instead of wiping the screen using `Console.Clear()`, it calculates differential offsets and selectively rewrites *only the individual characters that changed*, eliminating all high-frequency monitor flashing.

### 🎮 Component 2: Non-Blocking Real-Time Input Handler
Utilizes an asynchronous polling structure built over `Console.KeyAvailable`. This prevents the main execution loop (running at a tight 120ms tick rate) from locking or waiting for user actions, preserving rapid-fire responsive direction changes via WASD or Arrow Keys.

### 🍎 Component 3: Safe Procedural Spawning & Collision Matrix
A structural logic gate system checking for border thresholds and coordinate intersections. When the snake feasts on an apple, it dynamically tracks body elongation while ensuring new items spawn exclusively on unoccupied tiles.

---

## 🛠️ System Architecture Summary

### Prerequisites
- **.NET 10.0 SDK** or newer installed locally.
- A terminal environment supporting Monospaced fonts (e.g., Windows Terminal, Command Prompt set to *Consolas* or *Cascadia Code* at font size 20+ for the best visual experience).


### Step 1 — Project Setup

```bash
# Clone the repository and navigate to your downloaded target directory
git clone [https://github.com/Puneetas015/snake-game.git](https://github.com/Puneetas015/snake-game.git)
cd "C:\Users\Punit Tiwari\Downloads\snake game"

# Verify your .NET 10 SDK environment installation
dotnet --version
```
### Step 2 — Verify File Configurations
Ensure your directory contains exactly these two localized source files:
#### SnakeGame.csproj

```
XML
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>
</Project>

```
### Step 3 — Compile and Run the Game
Execute the global pipeline wrapper inside your terminal directory:
```bash
dotnet run
```
---

## Architecture Flow Summary
```bash
[Terminal KeyPress] ──► Console.KeyAvailable (Non-Blocking Engine)
                                   │
                                   ▼
   [Fixed-Rate Game Loop] ──► Update Snake Vector Direction ──► Check Collisions
                                                                      │
                                                                      ▼
   [Flicker-Free Renderer] ◄── Rewrite Dynamic Grid Coordinates ◄── [Wall / Self Matrix]
```
---

## Technical Specifications

| Parameter | Configuration Setting |
|---|---|
| **Target Runtime** | .NET 10.0 |
| **Tick Refresh Interval** | 120ms (Fixed Rate) |
| **Grid Boundary Dimensions** | 40 x 20 Units |
| **Input Schema** | W, A, S, D / Arrow Keys |
| **Rendering Protocol** | Double-buffered Differential Drawing |

---


## Troubleshooting

| Issue | Root Cause Analysis & Fix |
|---|---|
| `You must install or update .NET` | Target framework mismatch. Open `SnakeGame.csproj` and ensure `<TargetFramework>` matches your local SDK environment (`net10.0`). |
| Visual artifacts / overlapping characters | Ensure your terminal window is widened slightly beyond 40 columns before executing `dotnet run`. |
| Font looks squished or misaligned | Right-click your console window title bar -> Go to Properties -> Set font to **Consolas** or **Cascadia Code** with font size 20 or 24. |
