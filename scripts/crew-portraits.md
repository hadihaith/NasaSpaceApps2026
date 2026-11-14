# Crew portraits

The game uses the GLB rigs in `public/assets/characters` for the film and portraits sampled from their Idle clips for dialogue and recruitment.

After replacing a GLB under its existing filename, run `node scripts/render-crew-portraits.mjs` from the project directory. The script uses headless Chrome to write transparent WebP portraits to `public/homecoming/crew`. On macOS it tries Google Chrome in Applications. On other platforms it uses the `chromium` command. Set `CHROME_PATH` to the executable if you use another installation.

The same three filenames map to Commander Zorvax, Dr. Nyxen and Cadet B in `src/homecoming/Crew.tsx`.
