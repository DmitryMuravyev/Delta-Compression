// Builds Sprint_4_Full_Theory.pdf (A4) from theory.md.
// Needs pandoc, Node.js with playwright (Chromium) and the KaTeX dist folder:
//   npm pack katex@0.16.11 && tar xzf katex-0.16.11.tgz   -> package/dist
//   node build_theory.js [katex dist folder] [output pdf]
const { execFileSync } = require("child_process");
const fs = require("fs");
const os = require("os");
const path = require("path");
const { chromium } = require("playwright");

const here = __dirname;
const katex = path.resolve(process.argv[2] || path.join(here, "package", "dist")) + "/";
const output = path.resolve(process.argv[3] || path.join(here, "..", "Sprint_4_Full_Theory.pdf"));
const html = path.join(fs.mkdtempSync(path.join(os.tmpdir(), "theory-")), "theory.html");

execFileSync("pandoc", [path.join(here, "theory.md"), "-s", "--katex=" + katex,
  "-c", path.join(here, "theory.css"), "-o", html], { stdio: "inherit" });

(async () => {
  const browser = await chromium.launch();
  const page = await browser.newPage();
  await page.goto("file://" + html, { waitUntil: "load" });
  await page.waitForFunction(() => document.querySelectorAll("span.math").length ===
    document.querySelectorAll("span.math .katex").length);
  await page.pdf({ path: output, format: "A4", printBackground: true, preferCSSPageSize: true,
    displayHeaderFooter: true, headerTemplate: "<span></span>",
    footerTemplate: '<div style="font-size:8pt;width:100%;text-align:center;color:#666">' +
      '<span class="pageNumber"></span> / <span class="totalPages"></span></div>' });
  await browser.close();
  console.log("written: " + output);
})();
