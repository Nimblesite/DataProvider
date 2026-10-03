<!-- agent-pmo:795a9c2 -->
# Security Policy

GitHub surfaces this policy on the repository's **Security** tab and on the
"Report a vulnerability" page. References:
- Add a security policy: https://docs.github.com/en/code-security/how-tos/report-and-fix-vulnerabilities/configure-vulnerability-reporting/add-security-policy
- Configure private vulnerability reporting: https://docs.github.com/en/code-security/how-tos/report-and-fix-vulnerabilities/configure-vulnerability-reporting/configure-for-a-repository

## Reporting a Vulnerability

**Please do not report security vulnerabilities through public GitHub issues,
discussions, or pull requests.**

Report privately through GitHub's **private vulnerability reporting**: go to the
repository's **Security** tab → **Report a vulnerability** (or
<https://github.com/Nimblesite/DataProvider/security/advisories/new>). This opens
a private, structured advisory only the maintainers can see.

If you cannot use that channel, email **cftools@nimblesite.co**.

When reporting, please include:

- The type of issue (e.g. SQL injection, path traversal, auth bypass, secret exposure).
- The affected version(s), component (DataProvider, LQL, Migration, Sync, Gatekeeper, Reporting), file(s), and any relevant configuration.
- Steps to reproduce, ideally a minimal proof of concept.
- The impact: what an attacker can achieve.

## What to Expect

- **Acknowledgement** within **3 business days**.
- An assessment and a remediation plan (or a reasoned decline) within **10 business days**.
- Coordinated disclosure: we will agree a disclosure timeline with you and credit
  you in the advisory unless you prefer to remain anonymous.

## Supported Versions

DataProvider is currently in **beta**. Security fixes land on the latest released
`0.9.x` beta line; earlier prereleases are not supported.

| Version       | Supported |
| ------------- | --------- |
| 0.9.x (beta)  | ✅        |
| < 0.9 (beta)  | ❌        |
