from __future__ import annotations
from collections.abc import Callable
from dataclasses import dataclass, field
from kiota_abstractions.serialization import AdditionalDataHolder, Parsable, ParseNode, SerializationWriter
from typing import Any, Optional, TYPE_CHECKING, Union

if TYPE_CHECKING:
    from .qyl.domains.ops.deployment.deployment_environment import DeploymentEnvironment
    from .qyl.domains.ops.deployment.deployment_strategy import DeploymentStrategy

@dataclass
class DeploymentCreate(AdditionalDataHolder, Parsable):
    """
    Deployment creation request
    """
    # Stores additional data not described in the OpenAPI description found when deserializing. Can be used for serialization as well.
    additional_data: dict[str, Any] = field(default_factory=dict)

    # Deployed by
    deployed_by: Optional[str] = None
    # Environment
    environment: Optional[DeploymentEnvironment] = None
    # Git branch
    git_branch: Optional[str] = None
    # Git commit SHA
    git_commit: Optional[str] = None
    # Service name
    service_name: Optional[str] = None
    # Service version
    service_version: Optional[str] = None
    # Strategy
    strategy: Optional[DeploymentStrategy] = None
    
    @staticmethod
    def create_from_discriminator_value(parse_node: ParseNode) -> DeploymentCreate:
        """
        Creates a new instance of the appropriate class based on discriminator value
        param parse_node: The parse node to use to read the discriminator value and create the object
        Returns: DeploymentCreate
        """
        if parse_node is None:
            raise TypeError("parse_node cannot be null.")
        return DeploymentCreate()
    
    def get_field_deserializers(self,) -> dict[str, Callable[[ParseNode], None]]:
        """
        The deserialization information for the current model
        Returns: dict[str, Callable[[ParseNode], None]]
        """
        from .qyl.domains.ops.deployment.deployment_environment import DeploymentEnvironment
        from .qyl.domains.ops.deployment.deployment_strategy import DeploymentStrategy

        from .qyl.domains.ops.deployment.deployment_environment import DeploymentEnvironment
        from .qyl.domains.ops.deployment.deployment_strategy import DeploymentStrategy

        fields: dict[str, Callable[[Any], None]] = {
            "deployed_by": lambda n : setattr(self, 'deployed_by', n.get_str_value()),
            "environment": lambda n : setattr(self, 'environment', n.get_enum_value(DeploymentEnvironment)),
            "git_branch": lambda n : setattr(self, 'git_branch', n.get_str_value()),
            "git_commit": lambda n : setattr(self, 'git_commit', n.get_str_value()),
            "service_name": lambda n : setattr(self, 'service_name', n.get_str_value()),
            "service_version": lambda n : setattr(self, 'service_version', n.get_str_value()),
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
        writer.write_enum_value("environment", self.environment)
        writer.write_str_value("git_branch", self.git_branch)
        writer.write_str_value("git_commit", self.git_commit)
        writer.write_str_value("service_name", self.service_name)
        writer.write_str_value("service_version", self.service_version)
        writer.write_enum_value("strategy", self.strategy)
        writer.write_additional_data_value(self.additional_data)
    

