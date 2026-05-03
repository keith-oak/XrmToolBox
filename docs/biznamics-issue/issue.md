# Issue draft for Biznamics/PluginRegistration

**Title:** Please add an explicit LICENSE file to the repository

**Body:**

---

Hi maintainers,

The repository is listed as `Open Source: true` in the official XrmToolBox plugin catalogue (`https://www.xrmtoolbox.com/_odata/plugins`), and the README welcomes pull requests and bug reports. However, the repository does not contain a `LICENSE` file at root, and the GitHub API reports `"license": null` for the repo. The `.nuspec` file does not declare a `<license>` element.

Without an explicit licence, the GitHub Terms of Service give other users the right to view and fork the repository but not to redistribute it. This creates ambiguity for downstream projects (XrmToolBox community ports, alternative shells, packagers) that want to include the plugin in their distributions.

**Could you add an explicit `LICENSE` file at the repository root?**

A few common options:

- **MIT** — broadest reuse; matches what most XrmToolBox-adjacent libraries use.
- **GPL-3.0** — matches the XrmToolBox shell itself (`MscrmTools/XrmToolBox`) and the majority of the popular community plugins (Jonas Rapp's tooling, etc.).
- **Apache-2.0** — explicit patent grant, otherwise similar to MIT.

GitHub has a convenient flow: **Add file → Create new file → name it `LICENSE` → click "Choose a license template"**.

For context on the request: I'm working on a cross-platform (macOS/Linux) port of XrmToolBox and would like to bundle Plugin Registration in our distribution. The OData catalogue declaration is strong moral cover, but an explicit `LICENSE` file removes any legal ambiguity and protects you, us, and every other community packager.

A note on the original Microsoft CRM SDK sample lineage: roughly half of the source files still carry the original Microsoft "AS IS" header. Adding a project-level LICENSE for the Innofactor/Biznamics-authored portions and the years of community improvements is independent of, and does not conflict with, that history — the new licence covers your contributions and the project as a whole. Most projects in this lineage that have published on NuGet have done the same (for example, the Jonas Rapp tooling chose GPL-3.0).

Thank you for maintaining this critical piece of the Power Platform ecosystem.

---

**Where to file:** https://github.com/Biznamics/PluginRegistration/issues/new

**Action:** Keith to file when ready (we can't file under the project's account; this is a community request).
