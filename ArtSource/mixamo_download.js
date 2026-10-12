// Mixamo batch download for the hitman game (2026-10-11).
// 1. Log in at https://www.mixamo.com and pick any character (the script uses your current one; files are
//    exported "Without Skin", so only the skeleton matters - every Mixamo character has the same one).
// 2. Open the browser console (F12 > Console), paste this whole file, press Enter.
// 3. If the browser asks "Allow multiple downloads?", allow it. Leave the tab open until it says "All done".
// Files come out as "<name>.fbx" (FBX for Unity, without skin, 30 fps, In Place for the walks).
// Copy them into Assets/Art/Animations.
(async () => {
const ANIMS = [
  {"id": "c9c9f1c9-b96c-11e4-a802-0aaa78deedf9", "name": "Talking At Watercooler", "file": "Talking At Watercooler", "inPlace": false},
  {"id": "c9c63255-b96c-11e4-a802-0aaa78deedf9", "name": "Drinking", "file": "Drinking", "inPlace": false},
  {"id": "c9ca06f1-b96c-11e4-a802-0aaa78deedf9", "name": "Using A Filing Cabinet", "file": "Using A Filing Cabinet", "inPlace": false},
  {"id": "c9ca0631-b96c-11e4-a802-0aaa78deedf9", "name": "Searching Files High", "file": "Searching Files High", "inPlace": false},
  {"id": "c9ca04b6-b96c-11e4-a802-0aaa78deedf9", "name": "Looking Through Files Low", "file": "Looking Through Files Low", "inPlace": false},
  {"id": "c9ccab1b-b96c-11e4-a802-0aaa78deedf9", "name": "Sitting Idle", "file": "Sitting Idle", "inPlace": false},
  {"id": "c9cb9a91-b96c-11e4-a802-0aaa78deedf9", "name": "Sitting Drinking", "file": "Sitting Drinking", "inPlace": false},
  {"id": "c9cd7793-b96c-11e4-a802-0aaa78deedf9", "name": "Sitting Talking", "file": "Sitting Talking", "inPlace": false},
  {"id": "c9c70079-b96c-11e4-a802-0aaa78deedf9", "name": "Sitting Laughing", "file": "Sitting Laughing", "inPlace": false},
  {"id": "c9ccad56-b96c-11e4-a802-0aaa78deedf9", "name": "Sitting", "file": "Sitting - Sitting And Impatiently Waiting", "inPlace": false},
  {"id": "c9c698e8-b96c-11e4-a802-0aaa78deedf9", "name": "Sitting", "file": "Sitting - Sitting Looking Side To Side", "inPlace": false},
  {"id": "c9c9fa4d-b96c-11e4-a802-0aaa78deedf9", "name": "Texting While Standing", "file": "Texting While Standing", "inPlace": false},
  {"id": "c9c9fb33-b96c-11e4-a802-0aaa78deedf9", "name": "Texting", "file": "Texting", "inPlace": false},
  {"id": "c9ca0104-b96c-11e4-a802-0aaa78deedf9", "name": "Talking On A Cell Phone", "file": "Talking On A Cell Phone", "inPlace": false},
  {"id": "c9c9f63d-b96c-11e4-a802-0aaa78deedf9", "name": "Talking On Phone", "file": "Talking On Phone", "inPlace": false},
  {"id": "c9ca0249-b96c-11e4-a802-0aaa78deedf9", "name": "Talking Phone Pacing", "file": "Talking Phone Pacing", "inPlace": false},
  {"id": "c9c69770-b96c-11e4-a802-0aaa78deedf9", "name": "Leaning", "file": "Leaning", "inPlace": false},
  {"id": "c9c695f0-b96c-11e4-a802-0aaa78deedf9", "name": "One Shoulder Lean", "file": "One Shoulder Lean", "inPlace": false},
  {"id": "c9c603fd-b96c-11e4-a802-0aaa78deedf9", "name": "Smoking", "file": "Smoking", "inPlace": false},
  {"id": "c9ce59ae-b96c-11e4-a802-0aaa78deedf9", "name": "Drunk Walk", "file": "Drunk Walk", "inPlace": true},
  {"id": "c9ce58e5-b96c-11e4-a802-0aaa78deedf9", "name": "Drunk Idle", "file": "Drunk Idle", "inPlace": false},
  {"id": "c9ce60d0-b96c-11e4-a802-0aaa78deedf9", "name": "Drunk Idle Variation", "file": "Drunk Idle Variation", "inPlace": false},
  {"id": "c9c7972b-b96c-11e4-a802-0aaa78deedf9", "name": "Seated Idle", "file": "Seated Idle", "inPlace": false},
  {"id": "c9ccd3a9-b96c-11e4-a802-0aaa78deedf9", "name": "Sleeping Idle", "file": "Sleeping Idle", "inPlace": false},
  {"id": "c9ce6e3b-b96c-11e4-a802-0aaa78deedf9", "name": "Laying Sleeping", "file": "Laying Sleeping", "inPlace": false},
  {"id": "c9ce6cd3-b96c-11e4-a802-0aaa78deedf9", "name": "Laying Breathless", "file": "Laying Breathless", "inPlace": false},
  {"id": "c9c82b61-b96c-11e4-a802-0aaa78deedf9", "name": "Waking", "file": "Waking", "inPlace": false},
  {"id": "c9c6500d-b96c-11e4-a802-0aaa78deedf9", "name": "Talking", "file": "Talking", "inPlace": false},
  {"id": "c9c78799-b96c-11e4-a802-0aaa78deedf9", "name": "Bartending", "file": "Bartending", "inPlace": false},
  {"id": "c9c79c65-b96c-11e4-a802-0aaa78deedf9", "name": "Idle", "file": "Idle", "inPlace": false},
  {"id": "c9c79d1f-b96c-11e4-a802-0aaa78deedf9", "name": "Bored", "file": "Bored", "inPlace": false},
  {"id": "c9c84cf6-b96c-11e4-a802-0aaa78deedf9", "name": "Taking Item", "file": "Taking Item", "inPlace": false},
  {"id": "c9ca50c4-b96c-11e4-a802-0aaa78deedf9", "name": "Having A Meeting, Male", "file": "Having A Meeting Male", "inPlace": false},
  {"id": "c9ca5210-b96c-11e4-a802-0aaa78deedf9", "name": "Having A Meeting, Female", "file": "Having A Meeting Female", "inPlace": false},
  {"id": "c9c917a2-b96c-11e4-a802-0aaa78deedf9", "name": "Angry Gesture", "file": "Angry Gesture", "inPlace": false},
  {"id": "c9c916ce-b96c-11e4-a802-0aaa78deedf9", "name": "Angry Point", "file": "Angry Point", "inPlace": false},
  {"id": "c9cd7919-b96c-11e4-a802-0aaa78deedf9", "name": "Standing Arguing", "file": "Standing Arguing", "inPlace": false},
  {"id": "c9cd8d90-b96c-11e4-a802-0aaa78deedf9", "name": "Box Idle", "file": "Box Idle", "inPlace": false},
  {"id": "c9c64027-b96c-11e4-a802-0aaa78deedf9", "name": "Walking With Shopping Bag", "file": "Walking With Shopping Bag", "inPlace": true},
  {"id": "c9c9fd95-b96c-11e4-a802-0aaa78deedf9", "name": "Walking While Texting", "file": "Walking While Texting", "inPlace": true},
  {"id": "c9ccaece-b96c-11e4-a802-0aaa78deedf9", "name": "Stand To Sit", "file": "Stand To Sit", "inPlace": false},
  {"id": "c9ccabe2-b96c-11e4-a802-0aaa78deedf9", "name": "Sit To Stand", "file": "Sit To Stand", "inPlace": false},
  {"id": "c9ccb045-b96c-11e4-a802-0aaa78deedf9", "name": "Type To Sit", "file": "Type To Sit", "inPlace": false},
  {"id": "c9ccac9a-b96c-11e4-a802-0aaa78deedf9", "name": "Sit To Type", "file": "Sit To Type", "inPlace": false},
  {"id": "c9c6d86d-b96c-11e4-a802-0aaa78deedf9", "name": "Putting Down", "file": "Putting Down", "inPlace": false},
  {"id": "c9c8fe6e-b96c-11e4-a802-0aaa78deedf9", "name": "Stumble Backwards", "file": "Stumble Backwards", "inPlace": false},
  {"id": "c9c8d0fc-b96c-11e4-a802-0aaa78deedf9", "name": "Slipping", "file": "Slipping", "inPlace": false},
  {"id": "c9c62bc7-b96c-11e4-a802-0aaa78deedf9", "name": "Dying", "file": "Dying", "inPlace": false},
  {"id": "c9cdb289-b96c-11e4-a802-0aaa78deedf9", "name": "Falling Forward Death", "file": "Falling Forward Death", "inPlace": false},
  {"id": "c9c8ac04-b96c-11e4-a802-0aaa78deedf9", "name": "Kneeling Down", "file": "Kneeling Down", "inPlace": false},
  {"id": "c9c8cded-b96c-11e4-a802-0aaa78deedf9", "name": "Kneeling Inspecting", "file": "Kneeling Inspecting", "inPlace": false},
  {"id": "c9c6c5eb-b96c-11e4-a802-0aaa78deedf9", "name": "Picking Up Object", "file": "Picking Up Object", "inPlace": false},
  {"id": "c9c6d49e-b96c-11e4-a802-0aaa78deedf9", "name": "Opening", "file": "Opening", "inPlace": false}
];
const API = "https://www.mixamo.com/api/v1";
const token = localStorage.getItem("access_token");
if (!token) { console.error("Not logged in to Mixamo (no access_token). Log in and run this again."); return; }
const headers = { "Authorization": "Bearer " + token, "X-Api-Key": "mixamo2", "Content-Type": "application/json", "Accept": "application/json" };
const sleep = ms => new Promise(r => setTimeout(r, ms));
const get = async url => { const r = await fetch(url, { headers }); if (!r.ok) throw new Error(url + " -> " + r.status); return r.json(); };

const primary = await get(API + "/characters/primary");
const characterId = primary.primary_character_id;
console.log("Exporting on character:", primary.primary_character_name || characterId);

async function exportOne(a, format) {
  const product = await get(`${API}/products/${a.id}?similar=0&character_id=${characterId}`);
  const g = product.details.gms_hash;
  const gms = {
    "model-id": g["model-id"], "mirror": false, "trim": g.trim, "overdrive": 0,
    "params": (g.params || []).map(p => p[1]).join(","),
    "arm-space": g["arm-space"] || 0,
    "inplace": !!(a.inPlace && product.details.supports_inplace),
  };
  const body = { character_id: characterId, gms_hash: [gms], product_name: product.name, type: "Motion",
                 preferences: { format, skin: "false", fps: "30", reducekf: "0" } };
  const r = await fetch(API + "/animations/export", { method: "POST", headers, body: JSON.stringify(body) });
  if (!r.ok) throw new Error("export " + r.status + " " + await r.text());
  for (let i = 0; i < 120; i++) {                       // up to ~4 minutes
    await sleep(2000);
    const m = await get(`${API}/characters/${characterId}/monitor`);
    if (m.status === "completed") return m.job_result;
    if (m.status === "failed") throw new Error("export failed: " + JSON.stringify(m));
  }
  throw new Error("timed out");
}

async function save(url, name) {
  try {
    const blob = await (await fetch(url)).blob();
    const a = document.createElement("a");
    a.href = URL.createObjectURL(blob); a.download = name + ".fbx";
    document.body.appendChild(a); a.click(); a.remove();
    setTimeout(() => URL.revokeObjectURL(a.href), 10000);
  } catch (e) {                                          // CORS: let the browser download it directly
    const a = document.createElement("a"); a.href = url; a.download = name + ".fbx";
    document.body.appendChild(a); a.click(); a.remove();
  }
}

const failed = [];
for (let i = 0; i < ANIMS.length; i++) {
  const a = ANIMS[i];
  console.log(`(${i + 1}/${ANIMS.length}) ${a.file}...`);
  try {
    let url;
    try { url = await exportOne(a, "fbx7_unity"); }
    catch (e) { console.warn("  Unity FBX format refused, trying plain FBX:", e.message); url = await exportOne(a, "fbx7_2019"); }
    await save(url, a.file);
    await sleep(1500);
  } catch (e) { console.error("  FAILED " + a.file + ": " + e.message); failed.push(a.file); }
}
console.log(failed.length ? "All done, but these failed (download them by hand): " + failed.join(", ") : "All done - " + ANIMS.length + " animations downloaded.");
})();
