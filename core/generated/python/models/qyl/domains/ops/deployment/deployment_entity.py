from __future__ import annotations
import datetime
from collections.abc import Callable
from dataclasses import dataclass, field
from kiota_abstractions.serialization import AdditionalDataHolder, Parsable, ParseNode, SerializationWriter
from typing import Any, Optional, TYPE_CHECKING, Union

if TYPE_CHECKING:
    from .deployment_environment import DeploymentEnvironment
    from .deployment_status import DeploymentStatus
    from .deployment_strategy import DeploymentStrategy

@dataclass
class DeploymentEntity(AdditionalDataHolder, Parsable):
    """
    Complete deployment record
    """
    # Stores additional data not described in the OpenAPI description found when deserializing. Can be used for serialization as well.
    additional_data: dict[str, Any] = field(default_factory=dict)

    # Deployed by (user/system)
    deployed_by: Optional[str] = None
    # Deployment ID
    deployment_id: Optional[str] = None
    # Duration in seconds
    duration_s: Optional[float] = None
    # End time
    end_time: Optional[datetime.datetime] = None
    # Environment
    environment: Optional[DeploymentEnvironment] = None
    # Error message (if failed)
    error_message: Optional[str] = None
    # Git branch
    git_branch: Optional[str] = None
    # Git commit SHA
    git_commit: Optional[str] = None
    # Healthy replica count
    healthy_replicas: Optional[int] = None
    # Previous version
    previous_version: Optional[str] = None
    # Replica count
    replica_count: Optional[int] = None
    # Rollback target (if rolled back)
    rollback_target: Optional[str] = None
    # Service name
    service_name: Optional[str] = None
    # Service version
    service_version: Optional[str] = None
    # Start time
    start_time: Optional[datetime.datetime] = None
    # Status
    status: Optional[DeploymentStatus] = None
    # Strategy
    strategy: Optional[DeploymentStrategy] = None
    
    @staticmethod
    def create_from_discriminator_value(parse_node: ParseNode) -> DeploymentEntity:
        """
        Creates a new instance of the appropriate class based on discriminator value
        param parse_node: The parse node to use to read the discriminator value and create the object
        Returns: DeploymentEntity
        """
        if parse_node is None:
            raise TypeError("parse_node cannot be null.")
        return DeploymentEntity()
    
    def get_field_deserializers(self,) -> dict[str, Callable[[ParseNode], None]]:
        """
        The deserialization information for the current model
        Returns: dict[str, Callable[[ParseNode], None]]
        """
        from .deployment_environment import DeploymentEnvironment
        from .deployment_status import DeploymentStatus
        from .deployment_strategy import DeploymentStrategy

        from .deployment_environment import DeploymentEnvironment
        from .deployment_status import DeploymentStatus
        from .deployment_strategy import DeploymentStrategy

        fields: dict[str, Callable[[Any], None]] = {
            "deployed_by": lambda n : setattr(self, 'deployed_by', n.get_str_value()),
            "deployment.id": lambda n : setattr(self, 'deployment_id', n.get_str_value()),
            "duration_s": lambda n : setattr(self, 'duration_s', n.get_float_value()),
            "end_time": lambda n : setattr(self, 'end_time', n.get_datetime_value()),
            "environment": lambda n : setattr(self, 'environment', n.get_enum_value(DeploymentEnvironment)),
            "error_message": lambda n : setattr(self, 'error_message', n.get_str_value()),
            "git_branch": lambda n : setattr(self, 'git_branch', n.get_str_value()),
            "git_commit": lambda n : setattr(self, 'git_commit', n.get_str_value()),
            "healthy_replicas": lambda n : setattr(self, 'healthy_replicas', n.get_int_value()),
            "previous_version": lambda n : setattr(self, 'previous_version', n.get_str_value()),
            "replica_count": lambda n : setattr(self, 'replica_count', n.get_int_value()),
            "rollback_target": lambda n : setattr(self, 'rollback_target', n.get_str_value()),
            "service.name": lambda n : setattr(self, 'service_name', n.get_str_value()),
            "service.version": lambda n : setattr(self, 'service_version', n.get_str_value()),
            "start_time": lambda n : setattr(self, 'start_time', n.get_datetime_value()),
            "status": lambda n : setattr(self, 'status', n.get_enum_value(DeploymentStatus)),
            "strategy": lambda n : setattr(self, 'strategy', n.get_enum_value(DeploymentStrategy)),
        }
        return fields
    
    def serialize(self,writer: SerializationWriter) -> None:
        """
        Serializes information the current object
        param writer: Serialization writer to use to serialize this model
        Returns: None
        """
        if writer is None:
            raise TypeError("writer cannot be null.")
        writer.write_str_value("deployed_by", self.deployed_by)
        writer.write_str_value("deployment.id", self.deployment_id)
        writer.write_float_value("duration_s", self.duration_s)
        writer.write_datetime_value("end_time", self.end_time)
        writer.write_enum_value("environment", self.environment)
        writer.write_str_value("error_message", self.error_message)
        writer.write_str_value("git_branch", self.git_branch)
        writer.write_str_value("git_commit", self.git_commit)
        writer.write_int_value("healthy_replicas", self.healthy_replicas)
        writer.write_str_value("previous_version", self.previous_version)
        writer.write_int_value("replica_count", self.replica_count)
        writer.write_str_value("rollback_target", self.rollback_target)
        writer.write_str_value("service.name", self.service_name)
        writer.write_str_value("service.version", self.service_version)
        writer.write_datetime_value("start_time", self.start_time)
        writer.write_enum_value("status", self.status)
        writer.write_enum_value("strategy", self.strategy)
        writer.write_additional_data_value(self.additional_data)
    

