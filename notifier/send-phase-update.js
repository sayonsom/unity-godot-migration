#!/usr/bin/env node
// =============================================================================
// send-phase-update.js — Send migration phase updates to WhatsApp group
//
// Usage:
//   node send-phase-update.js <phase> <status> "<message>"
//
// Examples:
//   node send-phase-update.js 0 started "Beginning Unity dependency audit"
//   node send-phase-update.js 0 completed "Dependency matrix generated — 47 deps, 12 HIGH risk"
//   node send-phase-update.js 2 blocked "Unidot importer fails on complex prefabs"
//   node send-phase-update.js 3 completed "All 23 shaders rewritten as GDShader"
//
// Prerequisites: Run 'node setup.js' first to authenticate WhatsApp
// =============================================================================

const { Client, LocalAuth } = require("whatsapp-web.js");
const fs = require("fs");
const path = require("path");

// --- Phase definitions ---
const PHASES = {
  0: { name: "Unity Dependency Audit", duration: "2 weeks" },
  1: { name: "Godot Fork + Abstraction Layer", duration: "3 weeks" },
  2: { name: "Vertical Slice Migration", duration: "4 weeks" },
  3: { name: "Shader & Rendering Migration", duration: "6 weeks" },
  4: { name: "Accessibility + IoT Feature Build", duration: "6 weeks" },
  5: { name: "Full Migration + Platform Export", duration: "5 weeks" },
  test: { name: "Test Notification", duration: "N/A" },
};

// --- Status emoji/label mapping ---
const STATUS_MAP = {
  started: { emoji: "🟢", label: "STARTED" },
  completed: { emoji: "✅", label: "COMPLETED" },
  blocked: { emoji: "🔴", label: "BLOCKED" },
  "in-progress": { emoji: "🔵", label: "IN PROGRESS" },
  test: { emoji: "🧪", label: "TEST" },
};

// --- Parse args ---
const args = process.argv.slice(2);
if (args.length < 3) {
  console.log("Usage: node send-phase-update.js <phase> <status> \"<message>\"");
  console.log("");
  console.log("Phases: 0-5, test");
  console.log("Status: started, completed, blocked, in-progress, test");
  console.log("");
  console.log("Examples:");
  console.log(
    '  node send-phase-update.js 0 started "Beginning Unity dependency audit"'
  );
  console.log(
    '  node send-phase-update.js 3 completed "All shaders rewritten"'
  );
  process.exit(1);
}

const phaseKey = args[0];
const status = args[1].toLowerCase();
const message = args.slice(2).join(" ");

const phase = PHASES[phaseKey];
if (!phase) {
  console.error(`Unknown phase: ${phaseKey}. Valid: 0-5, test`);
  process.exit(1);
}

const statusInfo = STATUS_MAP[status] || { emoji: "ℹ️", label: status.toUpperCase() };

// --- Build message ---
const timestamp = new Date().toLocaleString("en-US", {
  timeZone: "America/Los_Angeles",
  year: "numeric",
  month: "short",
  day: "numeric",
  hour: "2-digit",
  minute: "2-digit",
});

const fullMessage = [
  `${statusInfo.emoji} *Unity→Godot Migration Update*`,
  ``,
  `*Phase ${phaseKey}:* ${phase.name}`,
  `*Status:* ${statusInfo.label}`,
  `*Timeline:* ${phase.duration}`,
  ``,
  `📝 ${message}`,
  ``,
  `🕐 ${timestamp}`,
  `━━━━━━━━━━━━━━━━━━━━━`,
  `_godot-smartthings-migration_`,
].join("\n");

console.log("Preparing to send:\n");
console.log(fullMessage);
console.log("");

// --- Load group config ---
const configPath = path.join(__dirname, ".group-config.json");
let groupId = null;
if (fs.existsSync(configPath)) {
  const config = JSON.parse(fs.readFileSync(configPath, "utf8"));
  groupId = config.groupId;
  console.log(`Target group: ${config.groupName} (${groupId})`);
} else {
  console.log("No .group-config.json found. Will search for Unity2Godot group...");
}

// --- Send via WhatsApp ---
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

let timeout = setTimeout(() => {
  console.error("❌ Timeout: WhatsApp client did not connect in 60 seconds.");
  console.log("Try: node setup.js (re-authenticate)");
  process.exit(1);
}, 60000);

client.on("ready", async () => {
  clearTimeout(timeout);
  console.log("WhatsApp connected.\n");

  try {
    let targetChat;

    if (groupId) {
      // Use saved group ID
      targetChat = await client.getChatById(groupId);
    } else {
      // Search for group
      const chats = await client.getChats();
      targetChat = chats.find(
        (c) => c.isGroup && c.name.toLowerCase().includes("unity2godot")
      );
    }

    if (!targetChat) {
      console.error('❌ Group "Unity2Godot" not found.');
      console.log("Create the group on WhatsApp first, then run: node setup.js");
      await client.destroy();
      process.exit(1);
    }

    // Send the message
    await targetChat.sendMessage(fullMessage);
    console.log(`✅ Message sent to "${targetChat.name}"!`);

    // Log to local file
    const logEntry = {
      timestamp: new Date().toISOString(),
      phase: phaseKey,
      phaseName: phase.name,
      status,
      message,
      group: targetChat.name,
    };

    const logFile = path.join(__dirname, "notification-log.json");
    let log = [];
    if (fs.existsSync(logFile)) {
      log = JSON.parse(fs.readFileSync(logFile, "utf8"));
    }
    log.push(logEntry);
    fs.writeFileSync(logFile, JSON.stringify(log, null, 2));
    console.log("📋 Logged to notification-log.json");
  } catch (err) {
    console.error("❌ Failed to send:", err.message);
  }

  await client.destroy();
  process.exit(0);
});

client.on("auth_failure", (msg) => {
  clearTimeout(timeout);
  console.error("❌ Auth failed:", msg);
  console.log("Re-run: node setup.js");
  process.exit(1);
});

client.initialize();
