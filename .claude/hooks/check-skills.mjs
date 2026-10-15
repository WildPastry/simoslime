// SessionStart hook. Plain stdout reaches the model as context. Prints nothing when all is well.
//   1. Every skill in .claude/skills-manifest.json is installed in ~/.claude/skills, and every
//      plugin it names is enabled in user, project or local settings. Missing ones: run
//      /setup-skills.
//   2. A repo whose CLAUDE.md carries the workflow loop between the two markers must carry the
//      same text as the machine-wide ~/.claude/CLAUDE.md, the canon. A repo with neither marker
//      holds addresses only and is not compared; a repo with one marker is broken and says so.
// Identical in every repo that shares the workflow. Never installs or edits anything.
import { existsSync, readdirSync, readFileSync } from "node:fs";
import { homedir } from "node:os";
import { join } from "node:path";

const root = process.env.CLAUDE_PROJECT_DIR || process.cwd();
const home = homedir();
const lines = [];

const readJson = (path) => (existsSync(path) ? JSON.parse(readFileSync(path, "utf8")) : null);
const report = (what, state, names) => {
  if (names.length > 0) {
    lines.push(`${what} this repo's workflow calls are not ${state}: ${names.join(", ")}. Run /setup-skills.`);
  }
};

const manifestPath = join(root, ".claude", "skills-manifest.json");
try {
  const manifest = readJson(manifestPath);
  if (manifest === null) {
    lines.push(`No skills manifest at ${manifestPath}, so the skills check did not run.`);
  } else {
    const skillsDir = join(home, ".claude", "skills");
    const installed = new Set(existsSync(skillsDir) ? readdirSync(skillsDir) : []);
    report(
      "Skills",
      "installed",
      (manifest.skills ?? []).filter((s) => !installed.has(s.name)).map((s) => s.name),
    );
    // A plugin may be enabled at user, project or local scope; the later scope wins.
    const scopes = [
      join(home, ".claude", "settings.json"),
      join(root, ".claude", "settings.json"),
      join(root, ".claude", "settings.local.json"),
    ];
    const enabled = Object.assign({}, ...scopes.map((path) => readJson(path)?.enabledPlugins ?? {}));
    report(
      "Plugins",
      "enabled",
      (manifest.plugins ?? []).filter((p) => enabled[p.id] !== true).map((p) => p.id),
    );
  }
} catch (error) {
  lines.push(`The skills check could not run: ${error.message}`);
}

const BEGIN = "<!-- workflow-loop:begin -->";
const END = "<!-- workflow-loop:end -->";
// null: the file carries no loop. undefined: a broken pair. Otherwise the marked text, with line
// endings normalised so a CRLF checkout compares equal.
const loopIn = (path) => {
  if (!existsSync(path)) return null;
  const text = readFileSync(path, "utf8");
  const a = text.indexOf(BEGIN);
  const b = text.indexOf(END);
  if (a === -1 && b === -1) return null;
  if (a === -1 || b === -1 || b < a) return undefined;
  return text.slice(a + BEGIN.length, b).replace(/\r\n/g, "\n").trim();
};
try {
  const repoLoop = loopIn(join(root, "CLAUDE.md"));
  if (repoLoop === undefined) {
    lines.push(
      "This repo's CLAUDE.md has only one workflow-loop marker. Restore the pair around the copied canon.",
    );
  } else if (repoLoop !== null) {
    const canonLoop = loopIn(join(home, ".claude", "CLAUDE.md"));
    if (canonLoop === undefined) {
      lines.push("~/.claude/CLAUDE.md has only one workflow-loop marker, so the loop cannot be compared.");
    } else if (canonLoop !== null && canonLoop !== repoLoop) {
      lines.push(
        "The workflow loop in this repo's CLAUDE.md differs from the machine-wide ~/.claude/CLAUDE.md. The machine-wide file is the canon: copy its marked section over this repo's before other work.",
      );
    }
  }
} catch (error) {
  lines.push(`The workflow-loop drift check could not run: ${error.message}`);
}

if (lines.length > 0) process.stdout.write(`${lines.join("\n")}\n`);
