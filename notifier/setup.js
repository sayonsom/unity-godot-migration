#!/usr/bin/env node
// =============================================================================
// setup.js — First-time WhatsApp authentication
// Run: cd notifier && npm install && node setup.js
// Scan the QR code with your phone's WhatsApp (Linked Devices > Link a Device)
// Session is saved to .wwebjs_auth/ — you only need to do this once.
// =============================================================================

const { Client, LocalAuth } = require("whatsapp-web.js");
const qrcode = require("qrcode-terminal");

console.log("==============================================");
console.log("  WhatsApp Authentication Setup");
console.log("  Unity→Godot Migration Notifier");
console.log("==============================================\n");
console.log("Waiting for QR code... Open WhatsApp on your phone:");
console.log("  Settings → Linked Devices → Link a Device\n");

const client = new Client({
  authStrategy: new LocalAuth({ dataPath: "./.wwebjs_auth" }),
  puppeteer: {
    headless: true,
    args: [
      "--no-sandbox",
      "--disable-setuid-sandbox",
      "--disable-dev-shm-usage",
      "--disable-gpu",
    ],
  },
});

client.on("qr", (qr) => {
  console.log("📱 Scan this QR code with your WhatsApp:\n");
  qrcode.generate(qr, { small: true });
  console.log("\nWaiting for scan...");
});

client.on("authenticated", () => {
  console.log("\n✅ Authenticated! Session saved to .wwebjs_auth/");
});

client.on("ready", async () => {
  console.log("✅ WhatsApp client is ready!\n");

  // Find the Unity2Godot group
  const chats = await client.getChats();
  const group = chats.find(
    (c) => c.isGroup && c.name.toLowerCase().includes("unity2godot")
  );

  if (group) {
    console.log(`✅ Found group: "${group.name}" (ID: ${group.id._serialized})`);
    console.log(`   Participants: ${group.participants?.length || "unknown"}`);

    // Save group ID for quick lookup
    const fs = require("fs");
    fs.writeFileSync(
      ".group-config.json",
      JSON.stringify(
        {
          groupId: group.id._serialized,
          groupName: group.name,
          savedAt: new Date().toISOString(),
        },
        null,
        2
      )
    );
    console.log("✅ Group config saved to .group-config.json");
  } else {
    console.log("\n⚠️  Group 'Unity2Godot' not found. Available groups:");
    const groups = chats.filter((c) => c.isGroup);
    groups.slice(0, 15).forEach((g) => {
      console.log(`   - "${g.name}" (ID: ${g.id._serialized})`);
    });
    console.log("\nCreate the group first, then re-run this setup.");
  }

  console.log("\n✅ Setup complete! You can now use:");
  console.log(
    '   node send-phase-update.js 0 "started" "Beginning Unity dependency audit"'
  );
  console.log("");

  await client.destroy();
  process.exit(0);
});

client.on("auth_failure", (msg) => {
  console.error("❌ Authentication failed:", msg);
  process.exit(1);
});

client.initialize();
