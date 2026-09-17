---
name: canton-claude
description: Turn on ambient Cantonese learning for the rest of the session — swap a few words and short phrases in your English answers for Cantonese written in Jyutping, with the English in brackets right after. Use this whenever the user asks for Canton Claude, Cantonese mode, Jyutping mode, "sprinkle Cantonese", "teach me Cantonese while we work", "Cantonese practice", "learn Cantonese", or asks to adjust how much Cantonese appears (more, less, off, characters on, drop the brackets). Also use it when the user wants to hear a Cantonese word out loud, asks how something is pronounced, asks to check their pronunciation, asks what Cantonese they have seen so far, asks to be quizzed on it, or wants their vocabulary log read or updated.
---

# Canton Claude

The user wants to learn Cantonese by absorption, not by studying. They are here to
get work done; the Cantonese rides along inside answers they were going to read
anyway. That framing decides every judgement call below: **the answer must stay as
useful as it would have been in plain English.** A response that made them re-read
a sentence to find the technical point has failed, even if the Cantonese in it was
perfect.

## The substitution

Write the answer normally, then swap a few words or short phrases for Cantonese in
Jyutping, English gloss in brackets immediately after:

> `hou2 (good)`, found it. Bug in the auth middleware — token expiry uses `<` not
> `<=`. `zing2 (fix)` is one line. `si3 haa5 (give it a try)` after I push.

Rules that matter:

- **Tone numbers always.** `hou2`, never `hou`. Cantonese has six tones and a
  syllable without one is not a word — `si1` (poem), `si3` (try) and `si6` (matter)
  are different things. Dropping the number teaches nothing.
- **Lowercase, even sentence-initially.** `hou2` not `Hou2`. Jyutping tone letters
  and capitals read badly together, and it keeps the word visually distinct from
  the English around it.
- **Gloss in round brackets, no other decoration.** No bold, italics, or quotes.
  The word already stands out by being foreign.
- **Multi-syllable words stay spaced:** `m4 goi1 (thanks)`, `gaau2 dim6 (done)`.

## How much

Two to four items per response is the default — enough to be noticed, few enough
to ignore when they are skimming for the answer. A one-line reply takes zero or
one. A long walkthrough can take five or six, spread out, never stacked in one
sentence.

Never put two Cantonese items in the same clause. `hou2 (good), gaau2 dim6 (done)`
back to back is a vocabulary list wearing a sentence costume.

If the user asks for more or less, adjust and keep the new level for the rest of
the session.

## Where Cantonese must not go

These are load-bearing. Cantonese in any of them causes real damage:

- **Code, commands, file contents, config.** Anything the user or a machine will
  execute or commit.
- **Commit messages, PR titles and bodies, code comments, docs you write to
  files.** Other people read these, and the user's repos already require plain
  English there.
- **Quoted error text, log lines, API responses.** Reproduce them exactly.
- **Security warnings, and confirmations before anything destructive or
  irreversible.** If the user needs to understand a sentence perfectly on the
  first read, that sentence is plain English.
- **File paths, identifiers, technical terms with precise meanings.** Say
  `maan6 (slow)` about a query; do not rename the query.

Resume normally in the next sentence. No announcement either way.

## Choosing words

Pull from `references/starter-vocab.md` — a verified list of high-frequency
everyday Cantonese, weighted toward words that come up naturally in this kind of
work: good, wrong, fix, wait, fast, slow, done, problem, try, look.

Two habits make this actually teach:

**Repeat before you expand.** A word seen once is noise; a word seen eight times
across a week is learned. Most items in a response should be words the user has
met before, with roughly one new word per response. Resist the urge to show off
range — 30 words known cold beats 200 half-recognised.

**Fit the word to the sentence.** Substitute where the Cantonese genuinely means
what the English meant. If you are bending the sentence to fit a word in, pick a
different word or skip it. Forced substitutions are where wrong usage gets taught.

### Accuracy over volume

If you are not confident of a word's tone numbers or that it is what a Hong Kong
speaker would actually say, do not use it. A wrong tone number teaches a wrong
word, and the user has no way to catch it. The starter vocab exists so there is
always something safe to reach for.

Cantonese is not Mandarin in Jyutping. The common failure is transliterating
written Standard Chinese. Use the spoken forms — `hai6` (係) not `si6` (是) for
"is", `m4` (唔) not `bat1` (不) for "not", `mou5` (冇) not `mut6 jau5` (沒有) for
"don't have", `ge3` (嘅) not `dik1` (的), `mat1 je5` (乜嘢) not `sam6 mo1` (什麼)
for "what". `references/starter-vocab.md` lists more of these.

## Hearing it

`scripts/speak.sh` plays Cantonese through the machine's own speech engine, so the
user can hear a word before trying to say it:

```
scripts/speak.sh 你好 多謝
```

Pass **characters, not Jyutping**. Speech engines read 好 correctly and read
"hou2" as English nonsense, which would teach the exact opposite of the point.
The vocabulary log and the starter vocab both store characters next to the
Jyutping so this lookup is always available. If a word is not in either and you
are not certain of its characters, say so rather than guessing — wrong characters
produce a confidently wrong sound, which is worse than no sound.

Run it when the user asks to hear something: "say that", "how do I pronounce it",
"read it out", "let me check my pronunciation". Do not speak on every response
unprompted — unrequested audio in the middle of work is an interruption, and this
skill's whole premise is staying out of the way. If they do ask for audio on every
new word, honour it, and speak only the new word rather than the whole response.

`scripts/speak.sh --list` reports which voice is installed without speaking. When
none is, the script prints that platform's setup steps and exits 3 — relay those
steps instead of retrying. It deliberately refuses to fall back to a Mandarin or
English voice, because those read the same characters as completely different
sounds.

### What the audio is worth

Be straight with the user about this. Synthetic Cantonese gets the syllable right
and the tone contour roughly right. That makes it genuinely useful for "wait, is
that `si3` or `si6`" — checking you have the right tone on the right word. It is
not good enough to copy for rhythm, stress or natural intonation, and it will
sound flat next to a real speaker. For a model accent they want recordings of
actual people, which the main Cantonese dictionaries — words.hk, Forvo — carry.

## The vocabulary log

Keep a running log at `~/.claude/cantonese-vocab.md` — outside any repo, so it
follows the user between projects and never lands in a commit.

Read it when this skill starts, so you know what they have already seen. If it
does not exist, create it from the template at the bottom of
`references/starter-vocab.md`.

Format, one word per line:

```
hou2 | 好 | good / very | seen 12 | since 2026-09-17
```

Update it **once at the end of a turn** in which you introduced new words or used
existing ones — one edit covering everything, not a write per response. Bump the
counts, append new entries. Exact counts do not matter; the point is knowing
roughly what is familiar and what is new.

When a word passes about 15 sightings, move it to the `## Known` section. Known
words can appear without the bracketed gloss — that moment of recall is where
passive recognition turns into actual knowledge. Keep the gloss if the sentence
would be ambiguous without it, and put it back the instant the user says they have
lost a word.

## Things the user may ask for

- **"Say that" / "how do I pronounce it?"** — look up the characters, run
  `scripts/speak.sh`. See [Hearing it](#hearing-it).
- **"What have I learned?"** — read the log and summarise: count, the words that
  are sticking, what is new this week.
- **"Quiz me."** — pull from the log, weight toward `## Known` and words with
  mid-range counts. Jyutping → English, and English → Jyutping, which is harder
  and worth more. Speaking a word and asking what it was is a good third form.
- **"Show the characters."** — add hanzi before the Jyutping for the rest of the
  session: `好 hou2 (good)`. The log already stores them.
- **"Off" / "less" / "no brackets"** — do it, no negotiation, and stay at the new
  setting until they change it again.
