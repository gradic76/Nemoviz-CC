# Nemoviz CC

Hrvatski niže · [English below](#english)

## Hrvatski

Nemoviz CC je mali prozor za razgovor s Claudeom, napravljen za rad s čitačima ekrana (JAWS i NVDA). Iza njega radi Claude Code, Anthropicov alat koji Claude koristi za rad s datotekama i programima na tvom računalu. Prozor je napravljen jer Claudeova desktop aplikacija i terminal s čitačima ekrana rade loše: gubi se fokus, čitaju se veliki komadi teksta, a živi ispis se miješa s ostalim.

### Preuzimanje

- Instalacija: [NemovizCC-Setup.exe](https://github.com/gradic76/Nemoviz-CC/releases/latest/download/NemovizCC-Setup.exe)
- Prijenosna inačica (bez instalacije, raspakiraj i pokreni NemovizCC.exe): [NemovizCC-portable.zip](https://github.com/gradic76/Nemoviz-CC/releases/latest/download/NemovizCC-portable.zip)

Program nije digitalno potpisan, pa Windows pri prvom pokretanju može pokazati upozorenje SmartScreen. Odaberi Više informacija, pa Ipak pokreni.

### Što treba

- Windows 10 ili 11, 64-bitni. .NET nije potrebno instalirati, ide uz program.
- Pretplata na Claude (Pro ili Max).
- Claude Code. Ako ga program ne nađe, ponudi službenu instalaciju. Ako koristiš Claudeovu desktop aplikaciju s Code dijelom, Claude Code već imaš.

### Prvo pokretanje

1. Program pita gdje će biti razgovori i projekti (zadano Dokumenti\Claude). U toj mapi podmapa Chat služi za obične razgovore, a svaka druga podmapa je jedan projekt.
2. Ako Claude Code nije prijavljen, odaberi Datoteka, Prijava u Claude. Otvorit će se konzola; prijava ide u pregledniku.

### Prozor

Odozgo: pokazatelji (kontekst, 5-satni i tjedni limit, model, napor, dopuštenja), Uživo (što Claude upravo radi), Povijest (cijeli razgovor) i na dnu Unos.

- Enter: pošalji. Shift+Enter: novi red. Alt+Enter: u red čekanja.
- Alt+1 do Alt+0: izgovori jednu od zadnjih deset poruka.
- U Povijesti: H sljedeća poruka, Shift+H prethodna.
- Alt+H Povijest, Alt+E Unos, Alt+L Uživo. Tab i Shift+Tab kroz sva polja.
- Polje Uživo govori samo dok je u njemu fokus.
- Kontekstni izbornik (tipka Aplikacije ili Shift+F10) u Povijesti: kopiraj poruku ili kod, spremi, otvori datoteke i mape spomenute u poruci. U Unosu: prilozi (Ctrl+U, ili zalijepi datoteke ili sliku).

### Izbornici

- Datoteka, Chat: Novi chat (naslov, upute, model, prilozi) i dosadašnji razgovori.
- Datoteka, Code: Novi projekt (čarobnjak), Otvori postojeću mapu i projekti s razgovorima.
- Datoteka, Razgovori i projekti: otvori, preimenuj i obriši (uvijek u koš za smeće).
- Datoteka, Postavke: jezik, tema, veličina slova, zvukovi, izgovor, upute za Claudea.
- Sesija: novi razgovor, nastavak nakon limita, predaja prije punjenja konteksta, brisanje tekućeg razgovora.

### Privatnost

Prozor ne šalje ništa sam. Razgovara samo s Claude Codeom na tvom računalu, a Claude Code s Anthropicom. Razgovori su u mapi .claude\projects u tvojoj korisničkoj mapi, iste kao za Claude Code i desktop aplikaciju. Postavke prozora su u %APPDATA%\ClaudeChat.

### Autori i licenca

Dizajn i projekt: Nemoguća vizija. Kodiranje: Claude (Anthropic).
Licenca MIT (LICENSE.txt). nvdaControllerClient.dll je dio NVDA-a (NV Access), licenca LGPL 2.1.

---

## English

Nemoviz CC is a small window for talking to Claude, built for screen readers (JAWS and NVDA). Behind it runs Claude Code, Anthropic's tool that lets Claude work with files and programs on your computer. It exists because Claude's desktop app and the terminal work poorly with screen readers: focus gets lost, large chunks of text are read at once, and live output mixes with everything else.

### Download

- Installer: [NemovizCC-Setup.exe](https://github.com/gradic76/Nemoviz-CC/releases/latest/download/NemovizCC-Setup.exe)
- Portable (no installation; unzip and run NemovizCC.exe): [NemovizCC-portable.zip](https://github.com/gradic76/Nemoviz-CC/releases/latest/download/NemovizCC-portable.zip)

The program is not digitally signed, so Windows SmartScreen may warn on first start. Choose More info, then Run anyway.

### Requirements

- Windows 10 or 11, 64-bit. No .NET installation needed; it comes with the program.
- A Claude subscription (Pro or Max).
- Claude Code. If the program cannot find it, it offers the official installer. If you use Claude's desktop app with its Code part, you already have it.

### First start

1. The program asks where conversations and projects live (Documents\Claude by default). Its Chat subfolder is for plain conversations; every other subfolder is one project.
2. If Claude Code is not signed in, choose File, Sign in to Claude. A console opens; signing in happens in the browser.

### The window

From the top: indicators (context, 5-hour and weekly limits, model, effort, permissions), Live (what Claude is doing right now), History (the whole conversation), and Message at the bottom.

- Enter: send. Shift+Enter: new line. Alt+Enter: queue.
- Alt+1 to Alt+0: speak one of the last ten messages.
- In History: H next message, Shift+H previous.
- Alt+H History, Alt+E Message, Alt+L Live. Tab and Shift+Tab through all fields.
- The Live field speaks only while it has the focus.
- Context menu (Applications key or Shift+F10) in History: copy a message or code, save, open files and folders mentioned in it. In Message: attachments (Ctrl+U, or paste files or an image).

### Menus

- File, Chat: New chat (title, instructions, model, attachments) and past conversations.
- File, Code: New project (wizard), Open an existing folder, and projects with their conversations.
- File, Conversations and projects: open, rename and delete (always to the Recycle Bin).
- File, Settings: language, theme, text size, sounds, speech, instructions for Claude.
- Session: new conversation, resume after the usage limit, hand over before the context fills, delete the current conversation.

### Privacy

The window sends nothing by itself. It talks only to Claude Code on your computer, and Claude Code to Anthropic. Conversations are kept in .claude\projects in your user folder, the same as for Claude Code and the desktop app. The window's settings are in %APPDATA%\ClaudeChat.

### Authors and license

Design and project: Nemoguća vizija. Coding: Claude (Anthropic).
MIT license (LICENSE.txt). nvdaControllerClient.dll is part of NVDA (NV Access), LGPL 2.1.
