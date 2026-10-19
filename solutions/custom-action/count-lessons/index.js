const fs = require("node:fs");
const path = require("node:path");

function getInput(name) {
  return (process.env[`INPUT_${name.toUpperCase()}`] || "").trim();
}

function setOutput(name, value) {
  fs.appendFileSync(process.env.GITHUB_OUTPUT, `${name}=${value}\n`);
}

function readLesson(file) {
  const text = fs.readFileSync(file, "utf8");
  const frontMatter = text.match(/^---\r?\n([\s\S]*?)\r?\n---/);
  if (!frontMatter) {
    return null;
  }

  const title = frontMatter[1].match(/^title:\s*"?(.*?)"?\s*$/m);
  const duration = frontMatter[1].match(/^duration:\s*(\d+)\s*$/m);
  const order = frontMatter[1].match(/^order:\s*(\d+)\s*$/m);
  if (!title || !duration) {
    return null;
  }

  return {
    title: title[1],
    minutes: Number(duration[1]),
    order: order ? Number(order[1]) : Number.MAX_SAFE_INTEGER,
  };
}

const folder = getInput("folder") || "lessons";
if (!fs.existsSync(folder)) {
  console.log(`::error::The folder ${folder} doesn't exist. Check the folder input.`);
  process.exit(1);
}

const lessons = fs
  .readdirSync(folder, { recursive: true, withFileTypes: true })
  .filter((entry) => entry.isFile() && entry.name.endsWith(".md"))
  .map((entry) => readLesson(path.join(entry.parentPath, entry.name)))
  .filter((lesson) => lesson !== null)
  .sort((a, b) => a.order - b.order);

const minutes = lessons.reduce((total, lesson) => total + lesson.minutes, 0);
console.log(`Found ${lessons.length} lessons in ${folder}, ${minutes} minutes in total.`);

setOutput("count", lessons.length);
setOutput("minutes", minutes);

const rows = lessons.map((lesson) => `| ${lesson.title.replaceAll("|", "\\|")} | ${lesson.minutes} |`);
const summary = [
  "## Lessons",
  "",
  "| Lesson | Minutes |",
  "| --- | ---: |",
  ...rows,
  `| **Total** | **${minutes}** |`,
  "",
].join("\n");
fs.appendFileSync(process.env.GITHUB_STEP_SUMMARY, summary);
