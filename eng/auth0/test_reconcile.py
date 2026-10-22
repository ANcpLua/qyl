import copy
import json
from pathlib import Path
import unittest

from reconcile import mapped_subjects, plan, reconcile


CONFIG = json.loads(Path(__file__).with_name("tenant.json").read_text())
ENV = {"MCP_COLLECTOR_PROJECTS": json.dumps([{"subjects": ["test|owner", "test|reviewer"]}])}


class FakeAuth0:
    def __init__(self):
        self.resources = [{"id": "api", **CONFIG["resource"], "scopes": [
            *CONFIG["resource"]["scopes"], {"value": "unrelated:scope", "description": "Preserve"}]}]
        self.clients = [{"client_id": name, "name": name} for name in CONFIG["user_client_names"]]
        self.grants = [{"id": name, "client_id": name, "audience": CONFIG["resource"]["identifier"],
                        "subject_type": "user", "scope": [*CONFIG["user_scopes"], "unrelated:scope"]}
                       for name in CONFIG["user_client_names"]]
        self.grants.append({"id": "default", "audience": CONFIG["resource"]["identifier"],
                            "subject_type": "user", "default_for": "third_party_clients",
                            **CONFIG["third_party_default"]})
        self.permissions = [{"resource_server_identifier": CONFIG["resource"]["identifier"],
                             "permission_name": scope} for scope in CONFIG["user_scopes"]]
        self.blocked = False
        self.flags = dict(CONFIG["tenant_flags"], unrelated_flag=True)

    def list(self, path, query=()):
        if path == "resource-servers": return copy.deepcopy(self.resources)
        if path == "clients": return copy.deepcopy(self.clients)
        if path == "client-grants": return copy.deepcopy(self.grants)
        if path.endswith("/permissions"): return copy.deepcopy(self.permissions)
        raise AssertionError(path)

    def request(self, method, path, data=None, query=()):
        self.assert_read(method)
        if path == "tenants/settings": return {"flags": copy.deepcopy(self.flags)}
        return {"user_id": path.removeprefix("users/").replace("%7C", "|"), "blocked": self.blocked}

    @staticmethod
    def assert_read(method):
        if method != "get": raise AssertionError("Planning must never mutate Auth0")


class WritableFakeAuth0(FakeAuth0):
    def __init__(self):
        super().__init__()
        self.mutations = []

    def request(self, method, path, data=None, query=()):
        if method == "get":
            return super().request(method, path, data, query)
        self.mutations.append((method, path, copy.deepcopy(data)))
        if method == "patch" and path.startswith("resource-servers/"):
            self.resources[0].update(copy.deepcopy(data))
        elif method == "patch" and path == "tenants/settings":
            self.flags.update(copy.deepcopy(data["flags"]))
        elif method == "patch" and path.startswith("client-grants/"):
            target = next(grant for grant in self.grants if path == f"client-grants/{grant['id']}")
            target.update(copy.deepcopy(data))
        else:
            raise AssertionError((method, path))


class ReconcileTests(unittest.TestCase):
    def test_matching_tenant_is_a_noop(self):
        self.assertEqual(plan(FakeAuth0(), CONFIG, ENV), ([], 2))

    def test_patch_preserves_unrelated_scopes(self):
        api = FakeAuth0()
        api.resources[0]["scopes"] = [{"value": "unrelated:scope", "description": "Preserve"}]
        api.grants[0]["scope"] = ["unrelated:scope"]
        changes, _ = plan(api, CONFIG, ENV)
        self.assertEqual(len(changes), 2)
        self.assertIn({"value": "unrelated:scope", "description": "Preserve"}, changes[0][3]["scopes"])
        self.assertEqual(set(changes[1][3]["scope"]), {*CONFIG["user_scopes"], "unrelated:scope"})

    def test_changed_scope_description_is_restored_and_unrelated_scope_kept(self):
        api = FakeAuth0()
        api.resources[0]["scopes"][0] = {"value": "qyl:read", "description": "read"}
        changes, _ = plan(api, CONFIG, ENV)
        self.assertEqual(len(changes), 1)
        self.assertEqual(changes[0][3]["scopes"], [{"value": "unrelated:scope", "description": "Preserve"},
                                                   *sorted(CONFIG["resource"]["scopes"], key=lambda s: s["value"])])

    def test_consent_flag_drift_patches_only_that_flag(self):
        api = WritableFakeAuth0()
        api.flags = {"unrelated_flag": True}
        self.assertEqual(plan(api, CONFIG, ENV)[0], [("tenant flags", "patch", "tenants/settings",
                                                       {"flags": {"use_scope_descriptions_for_consent": True}})])
        self.assertEqual(reconcile(api, CONFIG, ENV, apply=True, report=lambda _: None), 0)
        self.assertEqual(api.flags, {"unrelated_flag": True, "use_scope_descriptions_for_consent": True})

    def test_first_party_consent_skip_is_turned_off_and_scopes_kept(self):
        api = WritableFakeAuth0()
        api.resources[0]["skip_consent_for_verifiable_first_party_clients"] = True
        self.assertEqual(plan(api, CONFIG, ENV)[0], [("qyl API permissions", "patch", "resource-servers/api",
                                                       {"skip_consent_for_verifiable_first_party_clients": False})])
        self.assertEqual(reconcile(api, CONFIG, ENV, apply=True, report=lambda _: None), 0)
        self.assertFalse(api.resources[0]["skip_consent_for_verifiable_first_party_clients"])
        self.assertIn({"value": "unrelated:scope", "description": "Preserve"}, api.resources[0]["scopes"])

    def test_missing_grant_targets_only_verified_client(self):
        api = FakeAuth0()
        removed = api.grants.pop(0)
        changes, _ = plan(api, CONFIG, ENV)
        self.assertEqual(len(changes), 1)
        self.assertEqual(changes[0][1:3], ("post", "client-grants"))
        self.assertEqual(changes[0][3]["client_id"], removed["client_id"])

    def test_unknown_or_ambiguous_clients_fail_closed(self):
        for ambiguous in [False, True]:
            api = FakeAuth0()
            if ambiguous: api.clients.append(copy.deepcopy(api.clients[0]))
            else: api.clients.pop(0)
            with self.assertRaises(RuntimeError): plan(api, CONFIG, ENV)

    def test_missing_mapping_and_blocked_user_fail_closed(self):
        with self.assertRaises(RuntimeError): mapped_subjects(CONFIG, {})
        api = FakeAuth0()
        api.blocked = True
        with self.assertRaises(RuntimeError): plan(api, CONFIG, ENV)

    def test_only_missing_user_permissions_are_added(self):
        api = FakeAuth0()
        api.permissions.pop()
        changes, count = plan(api, CONFIG, ENV)
        self.assertEqual(count, 2)
        self.assertEqual(len(changes), 2)
        for _, method, path, body in changes:
            self.assertEqual(method, "post")
            self.assertTrue(path.endswith("/permissions"))
            self.assertEqual(body["permissions"], [{"resource_server_identifier": CONFIG["resource"]["identifier"],
                                                     "permission_name": "qyl:delete"}])

    def test_default_grant_restriction_does_not_touch_other_grants(self):
        api = FakeAuth0()
        api.grants[-1]["scope"] = ["qyl:read"]
        api.grants[-1]["allow_all_scopes"] = True
        changes, _ = plan(api, CONFIG, ENV)
        self.assertEqual(changes, [("qyl third-party default grant", "patch", "client-grants/default",
                                    CONFIG["third_party_default"])])

    def test_apply_rechecks_and_preserves_unmanaged_scopes(self):
        api = WritableFakeAuth0()
        api.resources[0]["scopes"] = [{"value": "unrelated:scope", "description": "Preserve"}]
        api.grants[0]["scope"] = ["unrelated:scope"]
        self.assertEqual(reconcile(api, CONFIG, ENV, apply=True, report=lambda _: None), 0)
        self.assertEqual(len(api.mutations), 2)
        self.assertEqual(plan(api, CONFIG, ENV), ([], 2))
        self.assertIn("unrelated:scope", api.grants[0]["scope"])
        self.assertEqual(reconcile(api, CONFIG, ENV, apply=True, report=lambda _: None), 0)
        self.assertEqual(len(api.mutations), 2, "second apply must be a no-op")

    def test_dry_run_and_failed_identity_never_mutate(self):
        api = WritableFakeAuth0()
        api.grants[0]["scope"] = []
        self.assertEqual(reconcile(api, CONFIG, ENV, report=lambda _: None), 1)
        self.assertEqual(api.mutations, [])
        api.blocked = True
        with self.assertRaises(RuntimeError):
            reconcile(api, CONFIG, ENV, apply=True, report=lambda _: None)
        self.assertEqual(api.mutations, [])


if __name__ == "__main__":
    unittest.main()
