# Deployment: Nasatrenddetective on Vercel

The GitHub Actions workflow `.github/workflows/nasatrenddetective.yml` checks, builds and deploys the app.

| Event | What runs |
| --- | --- |
| Pull request to `main` | Checks only: typecheck, lint, unit tests, Earth-data validation, production build |
| Push to `main` | Checks, then a **production** deploy to Vercel |
| Push to any other branch | Checks, then a **preview** deploy (its own URL) |
| Manual run (Actions tab → Nasatrenddetective → Run workflow) | Same as a push to the chosen branch |

Each deploy shows in the repository's **Deployments** panel (environments `production` and `preview`) with a link
to the live URL, and in the run's summary.

Why Vercel and not GitHub Pages: the app has server routes (`/api/timeseries` proxies NASA POWER, `/api/tiles` retries
NASA GIBS tiles). GitHub Pages only serves static files, so investigations would stop working there.

## One-time setup

Until the three secrets exist, the deploy job is skipped with a notice and the checks still run.

1. **Create a Vercel account** at https://vercel.com/signup. The free Hobby plan is enough. Signing up with GitHub is simplest.
2. **Create the project and get its IDs.** From the repository folder:
   ```bash
   npx vercel login
   ```
   ```bash
   npx vercel link --project nasatrenddetective
   ```
   Answer the prompts: create a new project named `nasatrenddetective`, and keep the default settings, since it detects Next.js.
   This writes `.vercel/project.json` (git-ignored) containing `orgId` and `projectId`:
   ```bash
   cat .vercel/project.json
   ```
3. **Create a token** at https://vercel.com/account/tokens (scope: your account; give it an expiry after the hackathon).
4. **Add the secrets to GitHub.** Each command prompts for the value, so it never appears in your shell history:
   ```bash
   gh secret set VERCEL_TOKEN
   ```
   ```bash
   gh secret set VERCEL_ORG_ID
   ```
   ```bash
   gh secret set VERCEL_PROJECT_ID
   ```
   Or add them under GitHub → Settings → Secrets and variables → Actions.
5. **Run it:** push to `main`, or start the workflow by hand from the Actions tab.

The production URL will be shown on the run and in Deployments. It is usually `https://nasatrenddetective.vercel.app`
if that name is free; otherwise Vercel adds a suffix.

## Notes

- `vercel.json` sets `git.deploymentEnabled: false`. That stops Vercel's own GitHub integration from deploying the
  same commit a second time; Actions is the only deployer.
- The app needs no API keys at runtime. NASA POWER and NASA GIBS are open services, and the Earth data in `public/data/` is bundled.
- The workflow does not re-download Earth data. It validates the committed `public/data/*.json` with
  `scripts/process_data.py validate`. To refresh the data, run `python3 scripts/process_data.py all` locally and commit.
- Never commit `.vercel/` or a token. `.vercel` is already in `.gitignore`.
