"""Publish TurboBoats to mod.io (game 5289 = Core Keeper).
Needs MODIO_TOKEN (OAuth write token) in the environment - the vault's MODIO_OAUTH_TOKEN in modio-api.env
(belongs to mod.io account matthewferry25, same owner as Minecart Turbo). Idempotent-ish: reuses an existing
mod with our name_id."""
import json, os, sys, urllib.request, urllib.parse, mimetypes, uuid

TOKEN = os.environ.get("MODIO_TOKEN") or sys.exit("MODIO_TOKEN missing")
API = os.environ.get("MODIO_API_URL", "https://api.mod.io/v1").rstrip("/")
GAME = 5289
NAME_ID = "turboboats"
HERE = os.path.dirname(os.path.abspath(__file__))
ZIP = os.path.join(HERE, "TurboBoats-1.2.0.zip")
LOGO = os.path.join(HERE, "turboboats-logo.png")
VERSION = "1.2.0"
CHANGELOG = "1.2.0 - ported to Core Keeper 1.3.0: boats are now registered as runtime EntityAuthoringDataBlocks (the 1.1.0 build fails to compile on 1.3.0). Pinned ObjectIDs 40001-40004 unchanged, so existing boats keep working. Previous: 1.1.0 - new top tier: Turbo Speeder (5x)."

SUMMARY = "Four craftable Turbo Speeders at the Boat Workbench: 2x, 3x, 4x and 5x the Speeder's speed, costing the same multiple of the materials."
DESCRIPTION = """<p><b>TURBO BOATS</b> adds four faster boats to the Boat Workbench. Each tier multiplies the vanilla Speeder's speed - and its crafting cost - by the same number.</p>
<table><tr><th>Item</th><th>Speed</th><th>Recipe (Boat Workbench)</th></tr>
<tr><td>Turbo Speeder (2x)</td><td>2x Speeder</td><td>200 Coral Plank, 160 Octarine Bar, 60 Gold Bar, 30 Mechanical Part</td></tr>
<tr><td>Turbo Speeder (3x)</td><td>3x Speeder</td><td>300 Coral Plank, 240 Octarine Bar, 90 Gold Bar, 45 Mechanical Part</td></tr>
<tr><td>Turbo Speeder (4x)</td><td>4x Speeder</td><td>400 Coral Plank, 320 Octarine Bar, 120 Gold Bar, 60 Mechanical Part</td></tr>
<tr><td>Turbo Speeder (5x)</td><td>5x Speeder</td><td>500 Coral Plank, 400 Octarine Bar, 150 Gold Bar, 75 Mechanical Part</td></tr></table>
<p>(Recipes always scale the game's CURRENT Speeder recipe - 1.3.0 rebalanced it to 100 Coral Plank / 80 Octarine Bar / 30 Gold Bar / 15 Mechanical Part, so the numbers above follow that.)</p>
<p>The boats look like the vanilla Speeder; the item name colour (Rare / Epic / Legendary) marks the tier.</p>
<p><b>Multiplayer:</b> must be installed on the server (host) and on every client - boat movement is predicted client-side, so both sides need the same boat data. Dedicated servers: add <code>turboboats</code> to the mods list. No CoreLib needed, no other dependencies. Works on Core Keeper 1.3.0 (1.3.0.6 tested); 1.1.0 was the last build for 1.2.1.5.</p>
<p><b>How it works:</b> at boot the mod clones the game's own Speeder definition four times, scales <code>speedMultiplier</code> and the recipe, registers the clones through the official mod API (at pinned ObjectIDs, so other mods can never shift them) and appends them to the Boat Workbench's recipe list.</p>
<p>Companion mod: <a href="https://mod.io/g/corekeeper/m/minecartturbo">Minecart Turbo</a> (same treatment for minecarts).</p>
<p>Source: <a href="https://github.com/mattferry/corekeeper-turbo-boats">mattferry/corekeeper-turbo-boats</a> (MIT). Written by Claude for the Ferry family server.</p>"""

def req(method, path, fields=None, files=None, json_body=None):
    url = API + path
    headers = {"Authorization": "Bearer " + TOKEN, "Accept": "application/json", "User-Agent": "curl/8.0 (TurboBoats publisher; oogley)"}
    data = None
    if files:
        boundary = uuid.uuid4().hex
        body = b""
        for k, v in (fields or {}).items():
            body += ("--%s\r\nContent-Disposition: form-data; name=\"%s\"\r\n\r\n%s\r\n" % (boundary, k, v)).encode()
        for k, (fn, content) in files.items():
            ctype = mimetypes.guess_type(fn)[0] or "application/octet-stream"
            body += ("--%s\r\nContent-Disposition: form-data; name=\"%s\"; filename=\"%s\"\r\nContent-Type: %s\r\n\r\n" % (boundary, k, fn, ctype)).encode() + content + b"\r\n"
        body += ("--%s--\r\n" % boundary).encode()
        headers["Content-Type"] = "multipart/form-data; boundary=" + boundary
        data = body
    elif fields is not None:
        data = urllib.parse.urlencode(fields, doseq=True).encode()
        headers["Content-Type"] = "application/x-www-form-urlencoded"
    r = urllib.request.Request(url, data=data, method=method, headers=headers)
    try:
        with urllib.request.urlopen(r) as resp:
            return resp.status, json.loads(resp.read() or b"{}")
    except urllib.error.HTTPError as e:
        return e.code, json.loads(e.read() or b"{}")

# 0. who am I
s, me = req("GET", "/me")
print("me:", s, me.get("username"), me.get("id"))
if s != 200: sys.exit("token rejected")

# 1. find or create the mod profile
s, d = req("GET", "/games/%d/mods?name_id=%s" % (GAME, NAME_ID))
mod = (d.get("data") or [None])[0]
if mod:
    print("existing mod:", mod["id"], mod["name"], "status", mod["status"], "visible", mod["visible"])
    s, e = req("PUT", "/games/%d/mods/%d" % (GAME, mod["id"]), fields={"summary": SUMMARY, "description": DESCRIPTION})
    print("update profile text:", s, "ok" if s == 200 else e)
else:
    s, mod = req("POST", "/games/%d/mods" % GAME,
                 fields={"name": "Turbo Boats", "name_id": NAME_ID, "summary": SUMMARY, "description": DESCRIPTION,
                         "visible": "1", "homepage_url": "https://github.com/mattferry/corekeeper-turbo-boats"},
                 files={"logo": ("turboboats-logo.png", open(LOGO, "rb").read())})
    print("create mod:", s, mod.get("id") or mod)
    if s not in (200, 201): sys.exit("mod creation failed")
MID = mod["id"]

# 2. upload the modfile
s, mf = req("POST", "/games/%d/mods/%d/files" % (GAME, MID),
            fields={"version": VERSION, "active": "true",
                    "changelog": CHANGELOG,
                    "metadata_blob": json.dumps({"requiredOn": 3})},
            files={"filedata": (os.path.basename(ZIP), open(ZIP, "rb").read())})
print("upload file:", s, (mf.get("id"), mf.get("version"), mf.get("virus_status")) if s in (200, 201) else mf)

# 3. tags (game version + type). Tag names must exist in the game's tag options.
s, opts = req("GET", "/games/%d/tags" % GAME)
available = {t for g in opts.get("data", []) for t in g.get("tags", [])}
want = [t for t in ["1.3.0", "Quality of Life", "Item", "Client", "Server", "Script"] if t in available]
s, t = req("POST", "/games/%d/mods/%d/tags" % (GAME, MID), fields={"tags[]": want})
print("tags:", s, want, t if s not in (200, 201) else "ok")

# 4. final state
s, final = req("GET", "/games/%d/mods/%d" % (GAME, MID))
print("FINAL:", final.get("id"), final.get("name_id"), "status", final.get("status"), "visible", final.get("visible"),
      "modfile", (final.get("modfile") or {}).get("version"), "url", final.get("profile_url"))
