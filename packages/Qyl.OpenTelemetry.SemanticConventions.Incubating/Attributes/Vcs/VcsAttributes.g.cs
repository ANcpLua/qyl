

#nullable enable

namespace Qyl.OpenTelemetry.SemanticConventions.Incubating.Attributes.Vcs;

public static class VcsAttributes
{
    public const string ChangeId = "vcs.change.id";

    public const string ChangeState = "vcs.change.state";

    public static class ChangeStateValues
    {
        public const string Closed = "closed";

        public const string Merged = "merged";

        public const string Open = "open";

        public const string Wip = "wip";
    }

    public const string ChangeTitle = "vcs.change.title";

    public const string LineChangeType = "vcs.line_change.type";

    public static class LineChangeTypeValues
    {
        public const string Added = "added";

        public const string Removed = "removed";
    }

    public const string OwnerName = "vcs.owner.name";

    public const string ProviderName = "vcs.provider.name";

    public static class ProviderNameValues
    {
        public const string Bitbucket = "bitbucket";

        public const string Gitea = "gitea";

        public const string Github = "github";

        public const string Gitlab = "gitlab";

        [global::System.Obsolete("{\"note\": \"Replaced by `gitea`.\", \"reason\": \"renamed\", \"renamed_to\": \"gitea\"}", false)]
        public const string Gittea = "gittea";
    }

    public const string RefBaseName = "vcs.ref.base.name";

    public const string RefBaseRevision = "vcs.ref.base.revision";

    public const string RefBaseType = "vcs.ref.base.type";

    public static class RefBaseTypeValues
    {
        public const string Branch = "branch";

        public const string Tag = "tag";
    }

    public const string RefHeadName = "vcs.ref.head.name";

    public const string RefHeadRevision = "vcs.ref.head.revision";

    public const string RefHeadType = "vcs.ref.head.type";

    public static class RefHeadTypeValues
    {
        public const string Branch = "branch";

        public const string Tag = "tag";
    }

    public const string RefType = "vcs.ref.type";

    public static class RefTypeValues
    {
        public const string Branch = "branch";

        public const string Tag = "tag";
    }

    [global::System.Obsolete("Replaced by vcs.change.id.", false)]
    public const string RepositoryChangeId = "vcs.repository.change.id";

    [global::System.Obsolete("Replaced by vcs.change.title.", false)]
    public const string RepositoryChangeTitle = "vcs.repository.change.title";

    public const string RepositoryName = "vcs.repository.name";

    [global::System.Obsolete("Replaced by vcs.ref.head.name.", false)]
    public const string RepositoryRefName = "vcs.repository.ref.name";

    [global::System.Obsolete("Replaced by vcs.ref.head.revision.", false)]
    public const string RepositoryRefRevision = "vcs.repository.ref.revision";

    [global::System.Obsolete("Replaced by vcs.ref.head.type.", false)]
    public const string RepositoryRefType = "vcs.repository.ref.type";

    public static class RepositoryRefTypeValues
    {
        public const string Branch = "branch";

        public const string Tag = "tag";
    }

    public const string RepositoryUrlFull = "vcs.repository.url.full";

    public const string RevisionDeltaDirection = "vcs.revision_delta.direction";

    public static class RevisionDeltaDirectionValues
    {
        public const string Ahead = "ahead";

        public const string Behind = "behind";
    }
}
