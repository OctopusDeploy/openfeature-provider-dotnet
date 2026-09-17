module.exports = {
  platform: 'github',
  timezone: 'Australia/Brisbane',
  requireConfig: 'optional',
  onboarding: false,

  repositories: ['OctopusDeploy/openfeature-provider-dotnet'],
  reviewers: ['team:team-devex'],
  branchPrefix: 'renovate/',

  // Limit concurrent PRs for better manageability
  prConcurrentLimit: 5,

  // Add labels to PRs
  labels: ['dependencies'],

  // Wait until a release has been published for at least 2 days before updating,
  // to avoid picking up versions that get yanked or hotfixed shortly after release.
  minimumReleaseAge: '2 days',

  // Enable vulnerability alerts
  osvVulnerabilityAlerts: true,

  // Extend with recommended config
  globalExtends: ['config:recommended'],

  // This repo gates PR titles (amannn/action-semantic-pull-request) and releases
  // via release-please, both of which require Conventional Commits. Force semantic
  // commit/PR titles (e.g. "chore(deps): ...", "fix(deps): ...") so Renovate PRs pass.
  semanticCommits: 'enabled',

  ignoreDeps: [
    'dotnet-sdk', // We don't want surprise .NET SDK updates in global.json.
  ],

  enabledManagers: ['nuget', 'github-actions'],

  packageRules: [
    {
      // pin/pinDigest/digest updates have no release timestamp, so
      // minimumReleaseAge can never be satisfied and its stability check
      // would leave those PRs pending forever.
      // https://github.com/renovatebot/renovate/issues/40288
      matchUpdateTypes: ['pin', 'pinDigest', 'digest'],
      minimumReleaseAge: null,
      prBodyNotes: [
        '**Manual supply-chain check:** `minimumReleaseAge` cannot be enforced for pin/digest updates because they have no release timestamp. Before merging, confirm this commit SHA has been published for at least **2 days**.',
      ],
    },
    {
      // Keep FluentAssertions on v7.x. v8 is no longer free.
      matchPackageNames: ['FluentAssertions'],
      allowedVersions: '7.x',
    },
    {
      // OpenFeature SDK updates are worth releasing as a minor version, even if not breaking
      matchPackageNames: ['OpenFeature'], 
      matchUpdateTypes: ['minor', 'patch'],
      semanticCommitType: 'feat',
    },
    {
      // Test and build tooling. Does not affect the shipped package. A green build gives us the
      // confidence we need. The label is what renovate-automerge.yml acts on.
      //
      // Only list packages that version independently of production code. Renovate evaluates these
      // rules per dependency and unions the labels onto the grouped PR, so a package that ships as
      // part of a monorepo (Microsoft.Extensions.Diagnostics.Testing, for one, which Renovate groups
      // with the dotnet monorepo alongside System.Text.Json) would label the whole group and
      // automerge the production upgrades riding along with it.
      matchPackageNames: [
        'coverlet.*',
        'xunit*',
        'Microsoft.NET.Test.Sdk',
        'WireMock.Net*',
        'GitHubActionsTestLogger',
      ],
      addLabels: ['automerge'],
    },
    {
      // renovatebot/github-action only runs in renovate.yml on a schedule, so CI never exercises
      // it and a green build proves nothing about the upgrade. Automerged anyway because the blast
      // radius is Renovate itself: if it breaks, dependency PRs stop appearing and nothing ships
      // wrong. Version updates only — `digest` is a separate update type, so the pure SHA
      // repoints below still get reviewed by hand.
      matchManagers: ['github-actions'],
      matchPackageNames: ['renovatebot/github-action'],
      matchUpdateTypes: ['minor', 'patch'],
      addLabels: ['automerge'],
    },
    {
      // The Octopus actions are skipped on renovate/ branches in build-test-pack-deploy, so CI
      // can't vouch for them and they stay on manual review. Group them instead, so a batch of
      // digest repoints is one PR to check rather than three.
      matchManagers: ['github-actions'],
      matchPackageNames: ['OctopusDeploy/**'],
      groupName: 'Octopus Deploy actions',
    },
    {
      // GitHub Actions: pin third-party actions to commit SHA for security.
      matchManagers: ['github-actions'],
      matchPackageNames: [
        '!actions/**', // Don't pin official GitHub actions (checkout, setup-dotnet, etc.).
      ],
      pinDigests: true,
    },
  ],
};
