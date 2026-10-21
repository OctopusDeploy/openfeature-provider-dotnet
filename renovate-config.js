module.exports = {
  extends: ['github>OctopusDeploy/team-bmbb-renovate-config:dotnet.json5'],

  // This repo gates PR titles (amannn/action-semantic-pull-request) and releases
  // via release-please, both of which require Conventional Commits. Force semantic
  // commit/PR titles (e.g. "chore(deps): ...", "fix(deps): ...") so Renovate PRs pass.
  semanticCommits: 'enabled',

  packageRules: [
    {
      // OpenFeature SDK updates are worth releasing as a minor version, even if not breaking
      matchPackageNames: ['OpenFeature'],
      matchUpdateTypes: ['minor', 'patch'],
      semanticCommitType: 'feat',
    },
    {
      // The Octopus actions are skipped on renovate/ branches in build-test-pack-deploy, so CI
      // can't vouch for them and they stay on manual review. Group them instead, so a batch of
      // digest repoints is one PR to check rather than three.
      matchManagers: ['github-actions'],
      matchPackageNames: ['OctopusDeploy/**'],
      groupName: 'Octopus Deploy actions',
    },
  ],
};
