# Changelog

## [4.0.0](https://github.com/OctopusDeploy/openfeature-provider-dotnet/compare/v3.0.0...v4.0.0) (2026-08-08)


### ⚠ BREAKING CHANGES

* Align type names with the other provider libraries ([#97](https://github.com/OctopusDeploy/openfeature-provider-dotnet/issues/97))
* Use new rules-based evaluations ([#96](https://github.com/OctopusDeploy/openfeature-provider-dotnet/issues/96))

### Features

* add v4 evaluation response types to provider library ([#92](https://github.com/OctopusDeploy/openfeature-provider-dotnet/issues/92)) ([2a8ce40](https://github.com/OctopusDeploy/openfeature-provider-dotnet/commit/2a8ce402c422a4cfe3bdff4d11abb8ceb5c92969))
* **deps:** update dependency openfeature to 2.14.0 ([#77](https://github.com/OctopusDeploy/openfeature-provider-dotnet/issues/77)) ([aba6950](https://github.com/OctopusDeploy/openfeature-provider-dotnet/commit/aba695060ac5fea649dfb32df3619fe7b44db520))
* implement v4 client-side flag evaluation ([#93](https://github.com/OctopusDeploy/openfeature-provider-dotnet/issues/93)) ([108e5ae](https://github.com/OctopusDeploy/openfeature-provider-dotnet/commit/108e5ae3ca1dbdcd4103ba7a4301d5c11b4d2b9d))
* Use new rules-based evaluations ([#96](https://github.com/OctopusDeploy/openfeature-provider-dotnet/issues/96)) ([a606077](https://github.com/OctopusDeploy/openfeature-provider-dotnet/commit/a606077d8bc72af57a55e49ee84c0a4e43d6b8d5))


### Code Refactoring

* Align type names with the other provider libraries ([#97](https://github.com/OctopusDeploy/openfeature-provider-dotnet/issues/97)) ([9a1225c](https://github.com/OctopusDeploy/openfeature-provider-dotnet/commit/9a1225cfc3c3dd08dc71572f1006cc62898a5307))

## [3.0.0](https://github.com/OctopusDeploy/openfeature-provider-dotnet/compare/v2.1.0...v3.0.0) (2026-06-14)


### ⚠ BREAKING CHANGES

* Return correct errors for unsupported flag types ([#58](https://github.com/OctopusDeploy/openfeature-provider-dotnet/issues/58))
* Send product metadata in custom header ([#52](https://github.com/OctopusDeploy/openfeature-provider-dotnet/issues/52))

### Features

* Send product metadata in custom header ([#52](https://github.com/OctopusDeploy/openfeature-provider-dotnet/issues/52)) ([a57a2ad](https://github.com/OctopusDeploy/openfeature-provider-dotnet/commit/a57a2adec68087790e1ebadbbb501a484e07d795))


### Bug Fixes

* Fallback to existing evaluation context on failed refresh ([#54](https://github.com/OctopusDeploy/openfeature-provider-dotnet/issues/54)) ([8435c1c](https://github.com/OctopusDeploy/openfeature-provider-dotnet/commit/8435c1c48b4e00f22fea806459172c76c2ae5915))
* log 'slug did not match' warning once per context ([#50](https://github.com/OctopusDeploy/openfeature-provider-dotnet/issues/50)) ([3d99baa](https://github.com/OctopusDeploy/openfeature-provider-dotnet/commit/3d99baa86d20f087c52f1dd6f4d341abfa6788f6))
* Remove slug formatting check from flag evaluation ([#57](https://github.com/OctopusDeploy/openfeature-provider-dotnet/issues/57)) ([8870cd3](https://github.com/OctopusDeploy/openfeature-provider-dotnet/commit/8870cd3765c1fa297b4bd9c51a18ef535de0232a))
* Return correct errors for unsupported flag types ([#58](https://github.com/OctopusDeploy/openfeature-provider-dotnet/issues/58)) ([2d71a85](https://github.com/OctopusDeploy/openfeature-provider-dotnet/commit/2d71a856e00f3e61f11d1a208a3a95295db5abb9))
* Simplify logic for refresh and retry ([#55](https://github.com/OctopusDeploy/openfeature-provider-dotnet/issues/55)) ([b3e3cf0](https://github.com/OctopusDeploy/openfeature-provider-dotnet/commit/b3e3cf019af66a877c4801207551a583630acb08))

## [2.1.0](https://github.com/OctopusDeploy/openfeature-provider-dotnet/compare/v2.0.0...v2.1.0) (2026-04-07)


### Features

* Add fractional evaluation for segments ([#45](https://github.com/OctopusDeploy/openfeature-provider-dotnet/issues/45)) ([ce08dee](https://github.com/OctopusDeploy/openfeature-provider-dotnet/commit/ce08dee04930c92740b7e86a88286429f881772c))

## [2.0.0](https://github.com/OctopusDeploy/openfeature-provider-dotnet/compare/v1.2.2...v2.0.0) (2026-01-27)


### ⚠ BREAKING CHANGES

* Add required user agent details ([#36](https://github.com/OctopusDeploy/openfeature-provider-dotnet/issues/36))
* Remove V2 toggle endpoint support ([#35](https://github.com/OctopusDeploy/openfeature-provider-dotnet/issues/35))

### Features

* Add required user agent details ([#36](https://github.com/OctopusDeploy/openfeature-provider-dotnet/issues/36)) ([10498f1](https://github.com/OctopusDeploy/openfeature-provider-dotnet/commit/10498f1f3129e06bb345f5dcac38faaf8d9a2500))


### Miscellaneous Chores

* Remove V2 toggle endpoint support ([#35](https://github.com/OctopusDeploy/openfeature-provider-dotnet/issues/35)) ([b52d7aa](https://github.com/OctopusDeploy/openfeature-provider-dotnet/commit/b52d7aab1ff4141d4d318e2322e6580b0d64f817))
