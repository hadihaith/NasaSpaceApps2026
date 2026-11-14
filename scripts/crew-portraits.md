# Crew portraits

The game uses the GLB rigs in `public/assets/characters` for the film and portraits sampled from their Idle clips for dialogue and recruitment.

After replacing a GLB under its existing filename, run `node scripts/render-crew-portraits.mjs` from the project directory. The script uses headless Chrome to write transparent WebP portraits to `public/homecoming/crew`. On macOS it tries Google Chrome in Applications. On other platforms it uses the `chromium` command. Set `CHROME_PATH` to the executable if you use another installation.

The filenames map to Commander Zorvax, Dr. Nyxen, Cadet B and Bogos Binted in `src/homecoming/Crew.tsx`.
To rebuild Bogos from source, run `node scripts/build-bogos-binted.mjs`, then render just that portrait with
`node scripts/render-crew-portraits.mjs bogos-binted`.
