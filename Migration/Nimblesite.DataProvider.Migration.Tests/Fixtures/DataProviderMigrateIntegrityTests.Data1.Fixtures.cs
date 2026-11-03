// Implements [CI-DESLOP-INDEPENDENT-DATA].
namespace Nimblesite.DataProvider.Migration.Tests;

public partial class DataProviderMigrateIntegrityTests
{
    private const string Data_Migrate_WhenDiffIsCleanButIntegrityFails_DoesNotClaimSchemaUpToDate_1 = """
        name: nap
        tables:
          - name: settings
            schema: public
            columns:
              - name: id
                type: Uuid
                isNullable: false
              - name: tier
                type: Text
                isNullable: false
                defaultValue: "'pro'"
            primaryKey:
              columns:
                - id
        """;

    private const string Data_CustomRegistrySchemaYaml_1 = """
        name: registry
        tables:
          - name: packages
            schema: registry
            columns:
              - name: id
                type: Uuid
                isNullable: false
              - name: name
                type: Text
                isNullable: false
            primaryKey:
              columns:
                - id
        """;

    private const string Data_TwoSchemaYaml_1 = """
        name: split
        tables:
          - name: packages
            schema: registry
            columns:
              - name: id
                type: Uuid
                isNullable: false
            primaryKey:
              columns:
                - id
          - name: audit
            schema: audit
            columns:
              - name: id
                type: Uuid
                isNullable: false
            primaryKey:
              columns:
                - id
        """;

    private const string Data_DesiredAgentConfigsYaml_1 = """
        name: nap
        tables:
          - name: agent_configs
            schema: public
            columns:
              - name: id
                type: Uuid
                isNullable: false
              - name: tenant_id
                type: Uuid
                isNullable: false
              - name: name
                type: Text
                isNullable: false
            primaryKey:
              columns:
                - id
        """;

    private const string Data_DesiredAgentConfigsWithUniqueYaml_1 = """
        name: nap
        tables:
          - name: agent_configs
            schema: public
            columns:
              - name: id
                type: Uuid
                isNullable: false
              - name: tenant_id
                type: Uuid
                isNullable: false
              - name: name
                type: Text
                isNullable: false
            primaryKey:
              columns:
                - id
            uniqueConstraints:
              - name: uq_agent_configs_tenant_name
                columns:
                  - tenant_id
                  - name
        """;

    private const string Data_OspreyRegistryOwnershipSchemaYaml_1 = """
        # Implements [PACKAGE-OWNERSHIP-DATABASE] and [PACKAGE-OWNERSHIP-ADMIN].
        # DataProviderMigrate owns DDL. PostgreSQL only; no hand-authored SQL overlay.
        # Bootstrap is deliberately closed: FORCE RLS, no allow policies/runtime grants.
        # Hosted write readiness must reject this state with ownership.not-ready.
        name: osprey_registry_ownership
        tables:
          - name: principals
            schema: registry
            columns:
              - { name: id, type: Uuid, isNullable: false }
              - { name: kind, type: Text, isNullable: false, checkConstraint: "kind IN ('human', 'workflow')" }
              - { name: state, type: Text, isNullable: false, checkConstraint: "state IN ('active', 'suspended')" }
              - { name: credential_epoch, type: BigInt, isNullable: false, checkConstraint: "credential_epoch > 0" }
              - { name: created_at, type: DateTimeOffset, isNullable: false }
            primaryKey: { columns: [id] }
            uniqueConstraints:
              - { name: uq_principals_id_kind, columns: [id, kind] }
            rowLevelSecurity: { enabled: true, forced: true, policies: [] }

          - name: auth_bindings
            schema: registry
            columns:
              - { name: id, type: Uuid, isNullable: false }
              - { name: principal_id, type: Uuid, isNullable: false }
              - { name: issuer, type: Text, isNullable: false }
              - { name: provider, type: Text, isNullable: false }
              - { name: provider_subject, type: Text, isNullable: false }
              - { name: state, type: Text, isNullable: false, checkConstraint: "state IN ('active', 'revoked')" }
              - { name: verified_at, type: DateTimeOffset, isNullable: false }
            primaryKey: { columns: [id] }
            uniqueConstraints:
              - { name: uq_auth_binding_identity, columns: [issuer, provider, provider_subject] }
            foreignKeys:
              - { columns: [principal_id], referencedSchema: registry, referencedTable: principals, referencedColumns: [id], onDelete: Restrict, onUpdate: Restrict }
            rowLevelSecurity: { enabled: true, forced: true, policies: [] }

          - name: organizations
            schema: registry
            columns:
              - { name: id, type: Uuid, isNullable: false }
              - { name: handle, type: Text, isNullable: false, checkConstraint: "length(handle) BETWEEN 1 AND 39 AND handle ~ '^[a-z0-9]([a-z0-9-]*[a-z0-9])?$'" }
              - { name: display_name, type: Text, isNullable: false }
              - { name: state, type: Text, isNullable: false, checkConstraint: "state IN ('active', 'frozen', 'tombstoned')" }
              - { name: governance_epoch, type: BigInt, isNullable: false, checkConstraint: "governance_epoch > 0" }
              - { name: created_at, type: DateTimeOffset, isNullable: false }
            primaryKey: { columns: [id] }
            uniqueConstraints:
              - { name: uq_organizations_handle, columns: [handle] }
            rowLevelSecurity: { enabled: true, forced: true, policies: [] }

          - name: memberships
            schema: registry
            columns:
              - { name: id, type: Uuid, isNullable: false }
              - { name: organization_id, type: Uuid, isNullable: false }
              - { name: principal_id, type: Uuid, isNullable: false }
              - { name: role, type: Text, isNullable: false, checkConstraint: "role IN ('custodian', 'member')" }
              - { name: principal_kind, type: Text, isNullable: false, checkConstraint: "principal_kind IN ('human', 'workflow')" }
              - { name: state, type: Text, isNullable: false, checkConstraint: "state IN ('active', 'revoked')" }
              - { name: authority_epoch, type: BigInt, isNullable: false, checkConstraint: "authority_epoch > 0" }
              - { name: accepted_at, type: DateTimeOffset, isNullable: false }
            primaryKey: { columns: [id] }
            uniqueConstraints:
              - { name: uq_memberships_org_id, columns: [organization_id, id] }
              - { name: uq_memberships_org_id_state_kind, columns: [organization_id, id, state, principal_kind] }
            checkConstraints:
              - { name: ck_custodian_human, expression: "role <> 'custodian' OR principal_kind = 'human'" }
            indexes:
              - { name: uq_memberships_org_principal_active, columns: [organization_id, principal_id], isUnique: true, filter: "state = 'active'" }
            foreignKeys:
              - { columns: [organization_id], referencedSchema: registry, referencedTable: organizations, referencedColumns: [id], onDelete: Restrict, onUpdate: Restrict }
              - { columns: [principal_id, principal_kind], referencedSchema: registry, referencedTable: principals, referencedColumns: [id, kind], onDelete: Restrict, onUpdate: Restrict }
            rowLevelSecurity: { enabled: true, forced: true, policies: [] }

          - name: recovery_authorities
            schema: registry
            columns:
              - { name: id, type: Uuid, isNullable: false }
              - { name: organization_id, type: Uuid, isNullable: false }
              - { name: principal_id, type: Uuid, isNullable: false }
              - { name: principal_kind, type: Text, isNullable: false, checkConstraint: "principal_kind = 'human'" }
              - { name: state, type: Text, isNullable: false, checkConstraint: "state IN ('active', 'revoked')" }
              - { name: governance_epoch, type: BigInt, isNullable: false, checkConstraint: "governance_epoch > 0" }
              - { name: enrollment_digest, type: Text, isNullable: false, checkConstraint: "enrollment_digest ~ '^sha256:[0-9a-f]{64}$'" }
              - { name: enrolled_at, type: DateTimeOffset, isNullable: false }
            primaryKey: { columns: [id] }
            indexes:
              - { name: uq_recovery_authorities_active, columns: [organization_id, principal_id], isUnique: true, filter: "state = 'active'" }
            foreignKeys:
              - { columns: [organization_id], referencedSchema: registry, referencedTable: organizations, referencedColumns: [id], onDelete: Restrict, onUpdate: Restrict }
              - { columns: [principal_id, principal_kind], referencedSchema: registry, referencedTable: principals, referencedColumns: [id, kind], onDelete: Restrict, onUpdate: Restrict }
            rowLevelSecurity: { enabled: true, forced: true, policies: [] }

          - name: packages
            schema: registry
            columns:
              - { name: id, type: Uuid, isNullable: false }
              - { name: organization_id, type: Uuid, isNullable: false }
              - { name: name, type: Text, isNullable: false, checkConstraint: "length(name) BETWEEN 1 AND 63 AND name ~ '^[a-z0-9]([a-z0-9-]*[a-z0-9])?$'" }
              - { name: admin_membership_id, type: Uuid, isNullable: false }
              - { name: admin_membership_state, type: Text, isNullable: false, checkConstraint: "admin_membership_state = 'active'" }
              - { name: admin_principal_kind, type: Text, isNullable: false, checkConstraint: "admin_principal_kind = 'human'" }
              - { name: authority_epoch, type: BigInt, isNullable: false, checkConstraint: "authority_epoch > 0" }
              - { name: state, type: Text, isNullable: false, checkConstraint: "state IN ('draft', 'active', 'write-frozen', 'deprecated', 'tombstoned')" }
              - { name: created_at, type: DateTimeOffset, isNullable: false }
            primaryKey: { columns: [id] }
            uniqueConstraints:
              - { name: uq_packages_org_name, columns: [organization_id, name] }
              - { name: uq_packages_org_id, columns: [organization_id, id] }
            foreignKeys:
              - { columns: [organization_id], referencedSchema: registry, referencedTable: organizations, referencedColumns: [id], onDelete: Restrict, onUpdate: Restrict }
              - { columns: [organization_id, admin_membership_id, admin_membership_state, admin_principal_kind], referencedSchema: registry, referencedTable: memberships, referencedColumns: [organization_id, id, state, principal_kind], onDelete: Restrict, onUpdate: Restrict }
            rowLevelSecurity: { enabled: true, forced: true, policies: [] }

          - name: package_permissions
            schema: registry
            columns:
              - { name: id, type: Uuid, isNullable: false }
              - { name: organization_id, type: Uuid, isNullable: false }
              - { name: package_id, type: Uuid, isNullable: false }
              - { name: membership_id, type: Uuid, isNullable: false }
              - { name: permission, type: Text, isNullable: false, checkConstraint: "permission IN ('publish-release', 'yank-release', 'edit-metadata', 'manage-maintenance', 'manage-security')" }
              - { name: state, type: Text, isNullable: false, checkConstraint: "state IN ('active', 'revoked')" }
              - { name: granted_by_membership_id, type: Uuid, isNullable: false }
              - { name: authority_epoch, type: BigInt, isNullable: false, checkConstraint: "authority_epoch > 0" }
              - { name: granted_at, type: DateTimeOffset, isNullable: false }
              - { name: expires_at, type: DateTimeOffset, isNullable: true }
            primaryKey: { columns: [id] }
            indexes:
              - { name: uq_package_permissions_subject_active, columns: [organization_id, package_id, membership_id, permission], isUnique: true, filter: "state = 'active'" }
            checkConstraints:
              - { name: ck_permission_expiry, expression: "expires_at IS NULL OR expires_at > granted_at" }
            foreignKeys:
              - { columns: [organization_id, package_id], referencedSchema: registry, referencedTable: packages, referencedColumns: [organization_id, id], onDelete: Restrict, onUpdate: Restrict }
              - { columns: [organization_id, membership_id], referencedSchema: registry, referencedTable: memberships, referencedColumns: [organization_id, id], onDelete: Restrict, onUpdate: Restrict }
              - { columns: [organization_id, granted_by_membership_id], referencedSchema: registry, referencedTable: memberships, referencedColumns: [organization_id, id], onDelete: Restrict, onUpdate: Restrict }
            rowLevelSecurity: { enabled: true, forced: true, policies: [] }

          - name: admin_transfers
            schema: registry
            columns:
              - { name: id, type: Uuid, isNullable: false }
              - { name: organization_id, type: Uuid, isNullable: false }
              - { name: package_id, type: Uuid, isNullable: false }
              - { name: from_membership_id, type: Uuid, isNullable: false }
              - { name: to_membership_id, type: Uuid, isNullable: false }
              - { name: to_membership_state, type: Text, isNullable: false, checkConstraint: "to_membership_state = 'active'" }
              - { name: to_principal_kind, type: Text, isNullable: false, checkConstraint: "to_principal_kind = 'human'" }
              - { name: expected_authority_epoch, type: BigInt, isNullable: false, checkConstraint: "expected_authority_epoch > 0" }
              - { name: state, type: Text, isNullable: false, checkConstraint: "state IN ('pending', 'completed', 'cancelled', 'declined', 'expired')" }
              - { name: proposal_digest, type: Text, isNullable: false, checkConstraint: "proposal_digest ~ '^sha256:[0-9a-f]{64}$'" }
              - { name: acceptance_digest, type: Text, isNullable: true, checkConstraint: "acceptance_digest IS NULL OR acceptance_digest ~ '^sha256:[0-9a-f]{64}$'" }
              - { name: created_at, type: DateTimeOffset, isNullable: false }
              - { name: not_before, type: DateTimeOffset, isNullable: false }
              - { name: expires_at, type: DateTimeOffset, isNullable: false }
              - { name: decided_at, type: DateTimeOffset, isNullable: true }
            primaryKey: { columns: [id] }
            checkConstraints:
              - { name: ck_admin_transfer_distinct, expression: "from_membership_id <> to_membership_id" }
              - { name: ck_admin_transfer_time, expression: "not_before >= created_at + INTERVAL '24 hours' AND expires_at > not_before" }
              - { name: ck_admin_transfer_acceptance, expression: "state <> 'completed' OR acceptance_digest IS NOT NULL" }
              - { name: ck_admin_transfer_decision, expression: "(state = 'pending' AND decided_at IS NULL) OR (state <> 'pending' AND decided_at IS NOT NULL AND decided_at >= created_at)" }
              - { name: ck_admin_transfer_completed_hold, expression: "state <> 'completed' OR (decided_at >= not_before AND decided_at < expires_at)" }
            indexes:
              - { name: uq_admin_transfer_pending, columns: [organization_id, package_id], isUnique: true, filter: "state = 'pending'" }
            foreignKeys:
              - { columns: [organization_id, package_id], referencedSchema: registry, referencedTable: packages, referencedColumns: [organization_id, id], onDelete: Restrict, onUpdate: Restrict }
              - { columns: [organization_id, from_membership_id], referencedSchema: registry, referencedTable: memberships, referencedColumns: [organization_id, id], onDelete: Restrict, onUpdate: Restrict }
              - { columns: [organization_id, to_membership_id, to_membership_state, to_principal_kind], referencedSchema: registry, referencedTable: memberships, referencedColumns: [organization_id, id, state, principal_kind], onDelete: Restrict, onUpdate: Restrict }
            rowLevelSecurity: { enabled: true, forced: true, policies: [] }

          - name: invitations
            schema: registry
            columns:
              - { name: id, type: Uuid, isNullable: false }
              - { name: organization_id, type: Uuid, isNullable: false }
              - { name: target_principal_id, type: Uuid, isNullable: false }
              - { name: issuer_membership_id, type: Uuid, isNullable: false }
              - { name: role, type: Text, isNullable: false, checkConstraint: "role IN ('custodian', 'member')" }
              - { name: token_digest, type: Text, isNullable: false, checkConstraint: "token_digest ~ '^sha256:[0-9a-f]{64}$'" }
              - { name: state, type: Text, isNullable: false, checkConstraint: "state IN ('pending', 'accepted', 'revoked', 'expired')" }
              - { name: expected_governance_epoch, type: BigInt, isNullable: false, checkConstraint: "expected_governance_epoch > 0" }
              - { name: created_at, type: DateTimeOffset, isNullable: false }
              - { name: expires_at, type: DateTimeOffset, isNullable: false }
            primaryKey: { columns: [id] }
            uniqueConstraints:
              - { name: uq_invitation_token, columns: [token_digest] }
            indexes:
              - { name: uq_invitation_pending_subject, columns: [organization_id, target_principal_id], isUnique: true, filter: "state = 'pending'" }
            checkConstraints:
              - { name: ck_invitation_expiry, expression: "expires_at > created_at" }
            foreignKeys:
              - { columns: [organization_id, issuer_membership_id], referencedSchema: registry, referencedTable: memberships, referencedColumns: [organization_id, id], onDelete: Restrict, onUpdate: Restrict }
              - { columns: [target_principal_id], referencedSchema: registry, referencedTable: principals, referencedColumns: [id], onDelete: Restrict, onUpdate: Restrict }
            rowLevelSecurity: { enabled: true, forced: true, policies: [] }

          - name: maintenance_reports
            schema: registry
            columns:
              - { name: id, type: Uuid, isNullable: false }
              - { name: organization_id, type: Uuid, isNullable: false }
              - { name: package_id, type: Uuid, isNullable: false }
              - { name: reporter_membership_id, type: Uuid, isNullable: false }
              - { name: summary, type: Text, isNullable: false, checkConstraint: "length(summary) BETWEEN 1 AND 4096" }
              - { name: evidence_digest, type: Text, isNullable: false, checkConstraint: "evidence_digest ~ '^sha256:[0-9a-f]{64}$'" }
              - { name: state, type: Text, isNullable: false, checkConstraint: "state IN ('open', 'acknowledged', 'resolved', 'withdrawn')" }
              - { name: created_at, type: DateTimeOffset, isNullable: false }
            primaryKey: { columns: [id] }
            foreignKeys:
              - { columns: [organization_id, package_id], referencedSchema: registry, referencedTable: packages, referencedColumns: [organization_id, id], onDelete: Restrict, onUpdate: Restrict }
              - { columns: [organization_id, reporter_membership_id], referencedSchema: registry, referencedTable: memberships, referencedColumns: [organization_id, id], onDelete: Restrict, onUpdate: Restrict }
            rowLevelSecurity: { enabled: true, forced: true, policies: [] }

          - name: governance_events
            schema: registry
            columns:
              - { name: id, type: Uuid, isNullable: false }
              - { name: organization_id, type: Uuid, isNullable: false }
              - { name: package_id, type: Uuid, isNullable: true }
              - { name: actor_principal_id, type: Uuid, isNullable: false }
              - { name: kind, type: Text, isNullable: false, checkConstraint: "kind IN ('organization-created', 'membership-changed', 'package-created', 'permission-changed', 'admin-proposed', 'admin-replaced', 'maintenance-changed', 'recovery', 'organization-transfer')" }
              - { name: authority_epoch, type: BigInt, isNullable: false, checkConstraint: "authority_epoch > 0" }
              - { name: proof_digest, type: Text, isNullable: false, checkConstraint: "proof_digest ~ '^sha256:[0-9a-f]{64}$'" }
              - { name: previous_event_digest, type: Text, isNullable: true, checkConstraint: "previous_event_digest IS NULL OR previous_event_digest ~ '^sha256:[0-9a-f]{64}$'" }
              - { name: created_at, type: DateTimeOffset, isNullable: false }
            primaryKey: { columns: [id] }
            foreignKeys:
              - { columns: [organization_id], referencedSchema: registry, referencedTable: organizations, referencedColumns: [id], onDelete: Restrict, onUpdate: Restrict }
              - { columns: [organization_id, package_id], referencedSchema: registry, referencedTable: packages, referencedColumns: [organization_id, id], onDelete: Restrict, onUpdate: Restrict }
              - { columns: [actor_principal_id], referencedSchema: registry, referencedTable: principals, referencedColumns: [id], onDelete: Restrict, onUpdate: Restrict }
            rowLevelSecurity: { enabled: true, forced: true, policies: [] }
        """;

    private const string Data_OspreyRegistryOwnershipTamperedPredicatesYaml_1 = """
        # Implements [PACKAGE-OWNERSHIP-DATABASE] and [PACKAGE-OWNERSHIP-ADMIN].
        # DataProviderMigrate owns DDL. PostgreSQL only; no hand-authored SQL overlay.
        # Bootstrap is deliberately closed: FORCE RLS, no allow policies/runtime grants.
        # Hosted write readiness must reject this state with ownership.not-ready.
        name: osprey_registry_ownership
        tables:
          - name: principals
            schema: registry
            columns:
              - { name: id, type: Uuid, isNullable: false }
              - { name: kind, type: Text, isNullable: false, checkConstraint: "kind IN ('human', 'workflow')" }
              - { name: state, type: Text, isNullable: false, checkConstraint: "state IN ('active', 'suspended')" }
              - { name: credential_epoch, type: BigInt, isNullable: false, checkConstraint: "credential_epoch > 0" }
              - { name: created_at, type: DateTimeOffset, isNullable: false }
            primaryKey: { columns: [id] }
            uniqueConstraints:
              - { name: uq_principals_id_kind, columns: [id, kind] }
            rowLevelSecurity: { enabled: true, forced: true, policies: [] }

          - name: auth_bindings
            schema: registry
            columns:
              - { name: id, type: Uuid, isNullable: false }
              - { name: principal_id, type: Uuid, isNullable: false }
              - { name: issuer, type: Text, isNullable: false }
              - { name: provider, type: Text, isNullable: false }
              - { name: provider_subject, type: Text, isNullable: false }
              - { name: state, type: Text, isNullable: false, checkConstraint: "state IN ('active', 'revoked')" }
              - { name: verified_at, type: DateTimeOffset, isNullable: false }
            primaryKey: { columns: [id] }
            uniqueConstraints:
              - { name: uq_auth_binding_identity, columns: [issuer, provider, provider_subject] }
            foreignKeys:
              - { columns: [principal_id], referencedSchema: registry, referencedTable: principals, referencedColumns: [id], onDelete: Restrict, onUpdate: Restrict }
            rowLevelSecurity: { enabled: true, forced: true, policies: [] }

          - name: organizations
            schema: registry
            columns:
              - { name: id, type: Uuid, isNullable: false }
              - { name: handle, type: Text, isNullable: false, checkConstraint: "length(handle) BETWEEN 1 AND 39 AND handle ~ '^[a-z0-9]([a-z0-9-]*[a-z0-9])?$'" }
              - { name: display_name, type: Text, isNullable: false }
              - { name: state, type: Text, isNullable: false, checkConstraint: "state IN ('active', 'frozen', 'tombstoned')" }
              - { name: governance_epoch, type: BigInt, isNullable: false, checkConstraint: "governance_epoch > 0" }
              - { name: created_at, type: DateTimeOffset, isNullable: false }
            primaryKey: { columns: [id] }
            uniqueConstraints:
              - { name: uq_organizations_handle, columns: [handle] }
            rowLevelSecurity: { enabled: true, forced: true, policies: [] }

          - name: memberships
            schema: registry
            columns:
              - { name: id, type: Uuid, isNullable: false }
              - { name: organization_id, type: Uuid, isNullable: false }
              - { name: principal_id, type: Uuid, isNullable: false }
              - { name: role, type: Text, isNullable: false, checkConstraint: "role IN ('custodian', 'member')" }
              - { name: principal_kind, type: Text, isNullable: false, checkConstraint: "principal_kind IN ('human', 'workflow')" }
              - { name: state, type: Text, isNullable: false, checkConstraint: "state IN ('active', 'revoked')" }
              - { name: authority_epoch, type: BigInt, isNullable: false, checkConstraint: "authority_epoch > 0" }
              - { name: accepted_at, type: DateTimeOffset, isNullable: false }
            primaryKey: { columns: [id] }
            uniqueConstraints:
              - { name: uq_memberships_org_id, columns: [organization_id, id] }
              - { name: uq_memberships_org_id_state_kind, columns: [organization_id, id, state, principal_kind] }
            checkConstraints:
              - { name: ck_custodian_human, expression: "role <> 'custodian' OR principal_kind = 'human'" }
            indexes:
              - { name: uq_memberships_org_principal_active, columns: [organization_id, principal_id], isUnique: true, filter: "state = 'pending'" }
            foreignKeys:
              - { columns: [organization_id], referencedSchema: registry, referencedTable: organizations, referencedColumns: [id], onDelete: Restrict, onUpdate: Restrict }
              - { columns: [principal_id, principal_kind], referencedSchema: registry, referencedTable: principals, referencedColumns: [id, kind], onDelete: Restrict, onUpdate: Restrict }
            rowLevelSecurity: { enabled: true, forced: true, policies: [] }

          - name: recovery_authorities
            schema: registry
            columns:
              - { name: id, type: Uuid, isNullable: false }
              - { name: organization_id, type: Uuid, isNullable: false }
              - { name: principal_id, type: Uuid, isNullable: false }
              - { name: principal_kind, type: Text, isNullable: false, checkConstraint: "principal_kind = 'human'" }
              - { name: state, type: Text, isNullable: false, checkConstraint: "state IN ('active', 'revoked')" }
              - { name: governance_epoch, type: BigInt, isNullable: false, checkConstraint: "governance_epoch > 0" }
              - { name: enrollment_digest, type: Text, isNullable: false, checkConstraint: "enrollment_digest ~ '^sha256:[0-9a-f]{64}$'" }
              - { name: enrolled_at, type: DateTimeOffset, isNullable: false }
            primaryKey: { columns: [id] }
            indexes:
              - { name: uq_recovery_authorities_active, columns: [organization_id, principal_id], isUnique: true, filter: "state = 'active'" }
            foreignKeys:
              - { columns: [organization_id], referencedSchema: registry, referencedTable: organizations, referencedColumns: [id], onDelete: Restrict, onUpdate: Restrict }
              - { columns: [principal_id, principal_kind], referencedSchema: registry, referencedTable: principals, referencedColumns: [id, kind], onDelete: Restrict, onUpdate: Restrict }
            rowLevelSecurity: { enabled: true, forced: true, policies: [] }

          - name: packages
            schema: registry
            columns:
              - { name: id, type: Uuid, isNullable: false }
              - { name: organization_id, type: Uuid, isNullable: false }
              - { name: name, type: Text, isNullable: false, checkConstraint: "length(name) BETWEEN 1 AND 63 AND name ~ '^[a-z0-9]([a-z0-9-]*[a-z0-9])?$'" }
              - { name: admin_membership_id, type: Uuid, isNullable: false }
              - { name: admin_membership_state, type: Text, isNullable: false, checkConstraint: "admin_membership_state = 'active'" }
              - { name: admin_principal_kind, type: Text, isNullable: false, checkConstraint: "admin_principal_kind = 'human'" }
              - { name: authority_epoch, type: BigInt, isNullable: false, checkConstraint: "authority_epoch > 0" }
              - { name: state, type: Text, isNullable: false, checkConstraint: "state IN ('draft', 'active', 'write-frozen', 'deprecated', 'tombstoned')" }
              - { name: created_at, type: DateTimeOffset, isNullable: false }
            primaryKey: { columns: [id] }
            uniqueConstraints:
              - { name: uq_packages_org_name, columns: [organization_id, name] }
              - { name: uq_packages_org_id, columns: [organization_id, id] }
            foreignKeys:
              - { columns: [organization_id], referencedSchema: registry, referencedTable: organizations, referencedColumns: [id], onDelete: Restrict, onUpdate: Restrict }
              - { columns: [organization_id, admin_membership_id, admin_membership_state, admin_principal_kind], referencedSchema: registry, referencedTable: memberships, referencedColumns: [organization_id, id, state, principal_kind], onDelete: Restrict, onUpdate: Restrict }
            rowLevelSecurity: { enabled: true, forced: true, policies: [] }

          - name: package_permissions
            schema: registry
            columns:
              - { name: id, type: Uuid, isNullable: false }
              - { name: organization_id, type: Uuid, isNullable: false }
              - { name: package_id, type: Uuid, isNullable: false }
              - { name: membership_id, type: Uuid, isNullable: false }
              - { name: permission, type: Text, isNullable: false, checkConstraint: "permission IN ('publish-release', 'yank-release', 'edit-metadata', 'manage-maintenance', 'manage-security')" }
              - { name: state, type: Text, isNullable: false, checkConstraint: "state IN ('active', 'revoked')" }
              - { name: granted_by_membership_id, type: Uuid, isNullable: false }
              - { name: authority_epoch, type: BigInt, isNullable: false, checkConstraint: "authority_epoch > 0" }
              - { name: granted_at, type: DateTimeOffset, isNullable: false }
              - { name: expires_at, type: DateTimeOffset, isNullable: true }
            primaryKey: { columns: [id] }
            indexes:
              - { name: uq_package_permissions_subject_active, columns: [organization_id, package_id, membership_id, permission], isUnique: true, filter: "state = 'active'" }
            checkConstraints:
              - { name: ck_permission_expiry, expression: "expires_at IS NULL OR expires_at > granted_at" }
            foreignKeys:
              - { columns: [organization_id, package_id], referencedSchema: registry, referencedTable: packages, referencedColumns: [organization_id, id], onDelete: Restrict, onUpdate: Restrict }
              - { columns: [organization_id, membership_id], referencedSchema: registry, referencedTable: memberships, referencedColumns: [organization_id, id], onDelete: Restrict, onUpdate: Restrict }
              - { columns: [organization_id, granted_by_membership_id], referencedSchema: registry, referencedTable: memberships, referencedColumns: [organization_id, id], onDelete: Restrict, onUpdate: Restrict }
            rowLevelSecurity: { enabled: true, forced: true, policies: [] }

          - name: admin_transfers
            schema: registry
            columns:
              - { name: id, type: Uuid, isNullable: false }
              - { name: organization_id, type: Uuid, isNullable: false }
              - { name: package_id, type: Uuid, isNullable: false }
              - { name: from_membership_id, type: Uuid, isNullable: false }
              - { name: to_membership_id, type: Uuid, isNullable: false }
              - { name: to_membership_state, type: Text, isNullable: false, checkConstraint: "to_membership_state = 'active'" }
              - { name: to_principal_kind, type: Text, isNullable: false, checkConstraint: "to_principal_kind = 'human'" }
              - { name: expected_authority_epoch, type: BigInt, isNullable: false, checkConstraint: "expected_authority_epoch > 0" }
              - { name: state, type: Text, isNullable: false, checkConstraint: "state IN ('pending', 'completed', 'cancelled', 'declined', 'expired')" }
              - { name: proposal_digest, type: Text, isNullable: false, checkConstraint: "proposal_digest ~ '^sha256:[0-9a-f]{64}$'" }
              - { name: acceptance_digest, type: Text, isNullable: true, checkConstraint: "acceptance_digest IS NULL OR acceptance_digest ~ '^sha256:[0-9a-f]{64}$'" }
              - { name: created_at, type: DateTimeOffset, isNullable: false }
              - { name: not_before, type: DateTimeOffset, isNullable: false }
              - { name: expires_at, type: DateTimeOffset, isNullable: false }
              - { name: decided_at, type: DateTimeOffset, isNullable: true }
            primaryKey: { columns: [id] }
            checkConstraints:
              - { name: ck_admin_transfer_distinct, expression: "from_membership_id <> to_membership_id" }
              - { name: ck_admin_transfer_time, expression: "not_before >= created_at + INTERVAL '12 hours' AND expires_at > not_before" }
              - { name: ck_admin_transfer_acceptance, expression: "state <> 'completed' OR acceptance_digest IS NOT NULL" }
              - { name: ck_admin_transfer_decision, expression: "(state = 'pending' AND decided_at IS NULL) OR (state <> 'pending' AND decided_at IS NOT NULL AND decided_at >= created_at)" }
              - { name: ck_admin_transfer_completed_hold, expression: "state <> 'completed' OR (decided_at >= not_before AND decided_at < expires_at)" }
            indexes:
              - { name: uq_admin_transfer_pending, columns: [organization_id, package_id], isUnique: true, filter: "state = 'pending'" }
            foreignKeys:
              - { columns: [organization_id, package_id], referencedSchema: registry, referencedTable: packages, referencedColumns: [organization_id, id], onDelete: Restrict, onUpdate: Restrict }
              - { columns: [organization_id, from_membership_id], referencedSchema: registry, referencedTable: memberships, referencedColumns: [organization_id, id], onDelete: Restrict, onUpdate: Restrict }
              - { columns: [organization_id, to_membership_id, to_membership_state, to_principal_kind], referencedSchema: registry, referencedTable: memberships, referencedColumns: [organization_id, id, state, principal_kind], onDelete: Restrict, onUpdate: Restrict }
            rowLevelSecurity: { enabled: true, forced: true, policies: [] }

          - name: invitations
            schema: registry
            columns:
              - { name: id, type: Uuid, isNullable: false }
              - { name: organization_id, type: Uuid, isNullable: false }
              - { name: target_principal_id, type: Uuid, isNullable: false }
              - { name: issuer_membership_id, type: Uuid, isNullable: false }
              - { name: role, type: Text, isNullable: false, checkConstraint: "role IN ('custodian', 'member')" }
              - { name: token_digest, type: Text, isNullable: false, checkConstraint: "token_digest ~ '^sha256:[0-9a-f]{64}$'" }
              - { name: state, type: Text, isNullable: false, checkConstraint: "state IN ('pending', 'accepted', 'revoked', 'expired')" }
              - { name: expected_governance_epoch, type: BigInt, isNullable: false, checkConstraint: "expected_governance_epoch > 0" }
              - { name: created_at, type: DateTimeOffset, isNullable: false }
              - { name: expires_at, type: DateTimeOffset, isNullable: false }
            primaryKey: { columns: [id] }
            uniqueConstraints:
              - { name: uq_invitation_token, columns: [token_digest] }
            indexes:
              - { name: uq_invitation_pending_subject, columns: [organization_id, target_principal_id], isUnique: true, filter: "state = 'pending'" }
            checkConstraints:
              - { name: ck_invitation_expiry, expression: "expires_at > created_at" }
            foreignKeys:
              - { columns: [organization_id, issuer_membership_id], referencedSchema: registry, referencedTable: memberships, referencedColumns: [organization_id, id], onDelete: Restrict, onUpdate: Restrict }
              - { columns: [target_principal_id], referencedSchema: registry, referencedTable: principals, referencedColumns: [id], onDelete: Restrict, onUpdate: Restrict }
            rowLevelSecurity: { enabled: true, forced: true, policies: [] }

          - name: maintenance_reports
            schema: registry
            columns:
              - { name: id, type: Uuid, isNullable: false }
              - { name: organization_id, type: Uuid, isNullable: false }
              - { name: package_id, type: Uuid, isNullable: false }
              - { name: reporter_membership_id, type: Uuid, isNullable: false }
              - { name: summary, type: Text, isNullable: false, checkConstraint: "length(summary) BETWEEN 1 AND 4096" }
              - { name: evidence_digest, type: Text, isNullable: false, checkConstraint: "evidence_digest ~ '^sha256:[0-9a-f]{64}$'" }
              - { name: state, type: Text, isNullable: false, checkConstraint: "state IN ('open', 'acknowledged', 'resolved', 'withdrawn')" }
              - { name: created_at, type: DateTimeOffset, isNullable: false }
            primaryKey: { columns: [id] }
            foreignKeys:
              - { columns: [organization_id, package_id], referencedSchema: registry, referencedTable: packages, referencedColumns: [organization_id, id], onDelete: Restrict, onUpdate: Restrict }
              - { columns: [organization_id, reporter_membership_id], referencedSchema: registry, referencedTable: memberships, referencedColumns: [organization_id, id], onDelete: Restrict, onUpdate: Restrict }
            rowLevelSecurity: { enabled: true, forced: true, policies: [] }

          - name: governance_events
            schema: registry
            columns:
              - { name: id, type: Uuid, isNullable: false }
              - { name: organization_id, type: Uuid, isNullable: false }
              - { name: package_id, type: Uuid, isNullable: true }
              - { name: actor_principal_id, type: Uuid, isNullable: false }
              - { name: kind, type: Text, isNullable: false, checkConstraint: "kind IN ('organization-created', 'membership-changed', 'package-created', 'permission-changed', 'admin-proposed', 'admin-replaced', 'maintenance-changed', 'recovery', 'organization-transfer')" }
              - { name: authority_epoch, type: BigInt, isNullable: false, checkConstraint: "authority_epoch > 0" }
              - { name: proof_digest, type: Text, isNullable: false, checkConstraint: "proof_digest ~ '^sha256:[0-9a-f]{64}$'" }
              - { name: previous_event_digest, type: Text, isNullable: true, checkConstraint: "previous_event_digest IS NULL OR previous_event_digest ~ '^sha256:[0-9a-f]{64}$'" }
              - { name: created_at, type: DateTimeOffset, isNullable: false }
            primaryKey: { columns: [id] }
            foreignKeys:
              - { columns: [organization_id], referencedSchema: registry, referencedTable: organizations, referencedColumns: [id], onDelete: Restrict, onUpdate: Restrict }
              - { columns: [organization_id, package_id], referencedSchema: registry, referencedTable: packages, referencedColumns: [organization_id, id], onDelete: Restrict, onUpdate: Restrict }
              - { columns: [actor_principal_id], referencedSchema: registry, referencedTable: principals, referencedColumns: [id], onDelete: Restrict, onUpdate: Restrict }
            rowLevelSecurity: { enabled: true, forced: true, policies: [] }
        """;
}
