# PKToy HTTP automation API

PKToy starts a loopback-only HTTP server with the desktop application. The default address is
`http://127.0.0.1:5180`. Use `--http-port 5190` or `--http-port=5190` to select another port.

All file and script paths are interpreted by the PKToy process. Callers should send URL-encoded
paths. Mutating operations use `POST`; read-only operations use `GET`. JSON command responses are
`{"ok":true}`. Screenshots are returned as `image/png`.

## Commands

| Method | Route | Query | Result |
| --- | --- | --- | --- |
| `GET` | `/api/health` | none | Status and supported standard views |
| `POST` | `/api/files/open` | `path` | Opens `.step`, `.stp`, `.x_t`, or `.x_b` and refreshes the view and topology tree |
| `POST` | `/api/files/save` | `path` | Saves the current session as `.x_t` or `.x_b` |
| `POST` | `/api/session/reset` | none | Resets the Parasolid session and clears the view |
| `POST` | `/api/geometry/cube` | none | Shows the UI's example cube geometry |
| `POST` | `/api/scripts/run` | `path` | Runs a `.csx` script and refreshes the view and topology tree |
| `GET` | `/api/topology` | none | Current bodies, topology nodes, and relations |
| `POST` | `/api/view/fit` | none | Fits the current part or assembly and resets pan/rotation |
| `POST` | `/api/view/orientation` | `name` | Fits and changes to a standard view |
| `POST` | `/api/view/rotate` | `yaw`, `pitch` | Applies a relative rotation in degrees |
| `POST` | `/api/view/zoom` | `delta` | Applies the same integer zoom delta used by the mouse wheel |
| `POST` | `/api/view/select` | `x`, `y` | Selects the primitive at physical-pixel coordinates |
| `GET` | `/api/screenshots/view` | optional `name` | Current or named 3D view screenshot |
| `GET` | `/api/screenshots/views` | none | One PNG containing all 14 standard views in a 4-column grid |
| `GET` | `/api/screenshots/window` | none | Entire PKToy client-area screenshot |

The 14 standard view names, in composite order, are:

`front`, `back`, `left`, `right`, `top`, `bottom`, `front-top-left`,
`front-top-right`, `front-bottom-left`, `front-bottom-right`, `back-top-left`,
`back-top-right`, `back-bottom-left`, and `back-bottom-right`.

## Examples

```bash
curl -X POST 'http://127.0.0.1:5180/api/files/open?path=third_party%2FPKToy%2Ftestmodels%2Fcone2.stp'
curl -X POST 'http://127.0.0.1:5180/api/view/orientation?name=front-top-right'
curl 'http://127.0.0.1:5180/api/screenshots/views' -o temp_docs/pktoy-14-views.png
curl 'http://127.0.0.1:5180/api/screenshots/window' -o temp_docs/pktoy-window.png
```

The server binds only to `127.0.0.1`; it is not exposed to the local network.
