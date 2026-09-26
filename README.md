# Realtime Tic-Tac-Toe Server

**A server-authoritative two-player tic-tac-toe host built with Unity Transport.** It assigns Player 1 / Player 2 on connect, validates moves against turn and board state, checks win/draw, and broadcasts board + result messages to both clients.

**Paired repository:** [realtime-tictactoe-client](https://github.com/PapiChulllo/realtime-tictactoe-client)

---

## How it works

1. First accepted connection becomes Player 1; second becomes Player 2. Further connections are closed.
2. On connect, the server sends `PLAYER|<n>` then the current board state (`rows|currentPlayer|gameActive`).
3. Clients request `MOVE|<claimedPlayer>|<x>|<y>`; the server **ignores** the claimed player number and uses the connection’s assigned number.
4. Valid empty-cell, on-turn moves update the board; win → `WIN|<player>`, full board → `DRAW`, then an updated state broadcast. Turns flip when the game continues.

**Status / limitations:** educational prototype. Player slots are a **lifetime counter** — disconnects do not free numbers or reset the match. `ResetGame` runs at server start only; there is no rematch protocol. Parsing is not hardened against malformed input. No auth, TLS, lobby, spectators, or persistence. Build Settings scene list is empty (Editor-only run path). No automated tests; Unity unavailable for re-verification in this documentation pass.

## Tech stack

| Area | What it uses |
|---|---|
| Engine | **Unity** `2022.3.46f1` (project under `TicTacToeServer/`) |
| Networking | **Unity Transport** `2.4.0` |
| Bind | UDP port **9001**, `NetworkEndpoint.AnyIpv4` |
| Encoding | `Encoding.Unicode` + `int` length prefix; reliable sequenced pipeline |
| Rules | In-process `int[3,3]` board (`0` empty, `1`/`2` players) |

A fragmentation-only pipeline is also created but unused by gameplay.

## What's in the project

| System | Key files |
|---|---|
| Transport lifecycle, player assignment, protocol, board rules, broadcasts | `TicTacToeServer/Assets/_Scripts/NetworkServer.cs` |
| Server Editor scene | `TicTacToeServer/Assets/Scenes/SampleScene.unity` |
| Packages / project settings | `TicTacToeServer/Packages/`, `TicTacToeServer/ProjectSettings/` |

One authored gameplay script (~8.7 KB) owns networking and rules end-to-end.

### Code / system highlights

- **Connection map:** `Dictionary<NetworkConnection, int> playerNumbers` with `nextPlayerNumber` capped at 2.
- **Authoritative move path:** maps sender → player, rejects off-turn / occupied / inactive games, then win/draw checks and `SendToAllClients`.
- **State payload:** three semicolon-separated rows of comma-separated cells, then current player and `gameActive` flag.

### Message protocol

| Direction | Payload | Purpose |
|---|---|---|
| Server → client | `PLAYER\|<1-or-2>` | Assign player number |
| Server → client | `<r0>;<r1>;<r2>\|<currentPlayer>\|<gameActive>` | Board + turn + active flag |
| Client → server | `MOVE\|<claimedPlayer>\|<x>\|<y>` | Move request (claimed player ignored) |
| Server → clients | `WIN\|<player>` / `DRAW` | Terminal result announcements |

## Scenes

| Scene | Purpose |
|---|---|
| `TicTacToeServer/Assets/Scenes/SampleScene.unity` | Open this Unity project folder and enter Play mode before clients connect |

Editor Build Settings scene list is empty — treat as Editor-only for this README.

## Third-party assets

Unity packages only (Transport, TextMesh Pro, uGUI, etc.). No third-party game content packs.

## About this repository

Public educational / portfolio showcase for a compact server-authoritative Unity Transport game under **PapiChulllo**. Pair with [realtime-tictactoe-client](https://github.com/PapiChulllo/realtime-tictactoe-client). Documentation mirrors committed source only.
