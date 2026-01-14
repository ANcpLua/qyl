// =============================================================================
// QYL v2.0 - VCS Semantic Conventions (OTel v1.39)
// =============================================================================
// Version Control System telemetry attributes.
// =============================================================================

import "../../common/types.tsp";

using Qyl.Common;

namespace Qyl.Domains.Data.Vcs;

// =============================================================================
// VCS Attributes
// =============================================================================

@doc("Version Control System attributes")
model VcsAttributes {
  @doc("VCS repository URL (without credentials)")
  @encodedName("application/json", "vcs.repository.url.full")
  repositoryUrlFull?: UrlString;

  @doc("Repository name")
  @encodedName("application/json", "vcs.repository.name")
  repositoryName?: string;

  @doc("Current branch name")
  @encodedName("application/json", "vcs.repository.ref.name")
  refName?: string;

  @doc("Reference type (branch, tag, etc.)")
  @encodedName("application/json", "vcs.repository.ref.type")
  refType?: VcsRefType;

  @doc("Commit revision (SHA)")
  @encodedName("application/json", "vcs.repository.ref.revision")
  refRevision?: string;

  @doc("VCS type (git, svn, etc.)")
  @encodedName("application/json", "vcs.type")
  vcsType?: VcsType;

  @doc("Change/PR ID")
  @encodedName("application/json", "vcs.change.id")
  changeId?: string;

  @doc("Change/PR title")
  @encodedName("application/json", "vcs.change.title")
  changeTitle?: string;

  @doc("Change/PR state")
  @encodedName("application/json", "vcs.change.state")
  changeState?: VcsChangeState;

  @doc("Change/PR base branch")
  @encodedName("application/json", "vcs.change.base.name")
  changeBaseName?: string;

  @doc("Change/PR head branch")
  @encodedName("application/json", "vcs.change.head.name")
  changeHeadName?: string;
}

// =============================================================================
// VCS Enumerations
// =============================================================================

@doc("VCS types")
enum VcsType {
  @doc("Git")
  git: "git",

  @doc("Subversion")
  svn: "svn",

  @doc("Mercurial")
  hg: "hg",

  @doc("Perforce")
  perforce: "perforce",

  @doc("CVS")
  cvs: "cvs",
}

@doc("VCS reference types")
enum VcsRefType {
  @doc("Branch reference")
  branch: "branch",

  @doc("Tag reference")
  tag: "tag",
}

@doc("VCS change/PR states")
enum VcsChangeState {
  @doc("Change is open")
  open: "open",

  @doc("Change is merged")
  merged: "merged",

  @doc("Change is closed (not merged)")
  closed: "closed",

  @doc("Change is in draft")
  draft: "draft",

  @doc("Change is pending review")
  pending: "pending",

  @doc("Change is approved")
  approved: "approved",

  @doc("Change has requested changes")
  changesRequested: "changes_requested",
}

@doc("VCS provider names")
enum VcsProviderName {
  @doc("GitHub")
  github: "github",

  @doc("GitLab")
  gitlab: "gitlab",

  @doc("Gitea")
  gitea: "gitea",

  @doc("Bitbucket")
  bitbucket: "bitbucket",

  @doc("Azure DevOps")
  azureDevops: "azure_devops",

  @doc("AWS CodeCommit")
  awsCodecommit: "aws_codecommit",

  @doc("Codeberg")
  codeberg: "codeberg",

  @doc("Sourcehut")
  sourcehut: "sourcehut",
}

// =============================================================================
// Git-Specific Attributes
// =============================================================================

@doc("Git-specific attributes")
model GitAttributes {
  @doc("Commit SHA (full 40 characters)")
  @encodedName("application/json", "git.commit.sha")
  @pattern("^[a-f0-9]{40}$")
  commitSha?: string;

  @doc("Commit author name")
  @encodedName("application/json", "git.commit.author.name")
  commitAuthorName?: string;

  @doc("Commit author email")
  @encodedName("application/json", "git.commit.author.email")
  commitAuthorEmail?: string;

  @doc("Commit timestamp")
  @encodedName("application/json", "git.commit.timestamp")
  commitTimestamp?: utcDateTime;

  @doc("Commit message (first line)")
  @encodedName("application/json", "git.commit.message")
  commitMessage?: string;

  @doc("Git remote name")
  @encodedName("application/json", "git.remote.name")
  remoteName?: string;

  @doc("Git remote URL")
  @encodedName("application/json", "git.remote.url")
  remoteUrl?: UrlString;

  @doc("Git worktree path")
  @encodedName("application/json", "git.worktree.path")
  worktreePath?: string;

  @doc("Whether repo has uncommitted changes")
  @encodedName("application/json", "git.dirty")
  dirty?: boolean;

  @doc("Number of commits ahead of remote")
  @encodedName("application/json", "git.ahead")
  ahead?: int32;

  @doc("Number of commits behind remote")
  @encodedName("application/json", "git.behind")
  behind?: int32;
}

// =============================================================================
// VCS Events
// =============================================================================

@doc("VCS event types")
enum VcsEventType {
  @doc("Push event")
  push: "push",

  @doc("Pull request opened")
  prOpened: "pr_opened",

  @doc("Pull request merged")
  prMerged: "pr_merged",

  @doc("Pull request closed")
  prClosed: "pr_closed",

  @doc("Branch created")
  branchCreated: "branch_created",

  @doc("Branch deleted")
  branchDeleted: "branch_deleted",

  @doc("Tag created")
  tagCreated: "tag_created",

  @doc("Tag deleted")
  tagDeleted: "tag_deleted",

  @doc("Commit")
  commit: "commit",

  @doc("Fork")
  fork: "fork",

  @doc("Release published")
  release: "release",
}

@doc("VCS event")
model VcsEvent {
  @doc("Event type")
  @encodedName("application/json", "event_type")
  eventType: VcsEventType;

  @doc("Repository information")
  repository: VcsAttributes;

  @doc("Git-specific info (if applicable)")
  git?: GitAttributes;

  @doc("Event timestamp")
  timestamp: utcDateTime;

  @doc("Actor/user who triggered the event")
  actor?: string;

  @doc("Event payload size")
  @encodedName("application/json", "payload_size")
  payloadSize?: ByteSize;
}
