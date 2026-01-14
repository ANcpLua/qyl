// =============================================================================
// QYL v2.0 - File Semantic Conventions (OTel v1.39)
// =============================================================================
// File system telemetry attributes.
// =============================================================================

import "../../common/types.tsp";

using Qyl.Common;

namespace Qyl.Domains.Data.File;

// =============================================================================
// File Attributes
// =============================================================================

@doc("File attributes")
model FileAttributes {
  @doc("File name with extension")
  @encodedName("application/json", "file.name")
  name?: string;

  @doc("Full file path")
  @encodedName("application/json", "file.path")
  path?: string;

  @doc("File extension (without dot)")
  @encodedName("application/json", "file.extension")
  extension?: string;

  @doc("File size in bytes")
  @encodedName("application/json", "file.size")
  size?: ByteSize;

  @doc("Directory containing the file")
  @encodedName("application/json", "file.directory")
  directory?: string;

  @doc("File MIME type")
  @encodedName("application/json", "file.mime_type")
  mimeType?: ContentType;

  @doc("File inode number")
  @encodedName("application/json", "file.inode")
  inode?: string;

  @doc("File owner username")
  @encodedName("application/json", "file.owner")
  owner?: string;

  @doc("File group name")
  @encodedName("application/json", "file.group")
  group?: string;

  @doc("File permissions (octal)")
  @encodedName("application/json", "file.mode")
  mode?: string;

  @doc("File creation time")
  @encodedName("application/json", "file.created")
  created?: utcDateTime;

  @doc("File modification time")
  @encodedName("application/json", "file.modified")
  modified?: utcDateTime;

  @doc("File access time")
  @encodedName("application/json", "file.accessed")
  accessed?: utcDateTime;

  @doc("File type")
  @encodedName("application/json", "file.type")
  fileType?: FileType;

  @doc("File hash (SHA-256)")
  @encodedName("application/json", "file.hash.sha256")
  hashSha256?: Sha256Hash;

  @doc("File hash (MD5)")
  @encodedName("application/json", "file.hash.md5")
  hashMd5?: string;

  @doc("Target path (for symlinks)")
  @encodedName("application/json", "file.target_path")
  targetPath?: string;
}

// =============================================================================
// File Enumerations
// =============================================================================

@doc("File types")
enum FileType {
  @doc("Regular file")
  file: "file",

  @doc("Directory")
  directory: "directory",

  @doc("Symbolic link")
  symlink: "symlink",

  @doc("Block device")
  block: "block",

  @doc("Character device")
  character: "character",

  @doc("Named pipe (FIFO)")
  pipe: "pipe",

  @doc("Unix socket")
  socket: "socket",
}

@doc("File operation types")
enum FileOperation {
  @doc("File created")
  create: "create",

  @doc("File deleted")
  delete: "delete",

  @doc("File modified")
  modify: "modify",

  @doc("File renamed")
  rename: "rename",

  @doc("File moved")
  move: "move",

  @doc("File read")
  read: "read",

  @doc("File written")
  write: "write",

  @doc("File opened")
  open: "open",

  @doc("File closed")
  close: "close",

  @doc("File permissions changed")
  chmod: "chmod",

  @doc("File ownership changed")
  chown: "chown",

  @doc("File accessed")
  access: "access",
}

// =============================================================================
// File Event Model
// =============================================================================

@doc("File system event")
model FileEvent {
  @doc("File information")
  file: FileAttributes;

  @doc("Operation type")
  operation: FileOperation;

  @doc("Event timestamp")
  timestamp: utcDateTime;

  @doc("Process that performed the operation")
  @encodedName("application/json", "process.pid")
  processPid?: int64;

  @doc("User that performed the operation")
  @encodedName("application/json", "user.name")
  userName?: string;

  @doc("Original path (for rename/move)")
  @encodedName("application/json", "original_path")
  originalPath?: string;
}

// =============================================================================
// File Metrics
// =============================================================================

@doc("File system operations metric")
model FileOperationsMetric {
  @doc("Metric name")
  name: "file.operations";

  @doc("Metric unit")
  unit: "{operation}";

  @doc("File operation type")
  @encodedName("application/json", "file.operation")
  operation: FileOperation;

  @doc("File path pattern")
  @encodedName("application/json", "file.path.pattern")
  pathPattern?: string;
}

@doc("File IO bytes metric")
model FileIoBytesMetric {
  @doc("Metric name")
  name: "file.io";

  @doc("Metric unit (bytes)")
  unit: "By";

  @doc("IO direction")
  @encodedName("application/json", "file.io.direction")
  direction: string;

  @doc("File path pattern")
  @encodedName("application/json", "file.path.pattern")
  pathPattern?: string;
}
