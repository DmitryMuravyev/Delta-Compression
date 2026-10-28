# Sprint 4 document sources

`deck.py` builds `../Sprint_4_Mathematical_foundation.pdf`: the slides are drawn with matplotlib
(Liberation Sans for text, Computer Modern for formulas) and `title.pdf` is put in front of them with pypdf.
`model.py` is a small Python model of the block cost formulas used for the worked examples.

    python3 deck.py ../Sprint_4_Mathematical_foundation.pdf

`theory.md` is the A4 theory document with references to the code. `build_theory.js` turns it into
`../Sprint_4_Full_Theory.pdf` with pandoc, KaTeX (`theory.css` is the page style) and Playwright Chromium:

    npm pack katex@0.16.11 && tar xzf katex-0.16.11.tgz
    node build_theory.js package/dist ../Sprint_4_Full_Theory.pdf
