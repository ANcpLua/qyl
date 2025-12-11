from __future__ import annotations
from collections.abc import Callable
from dataclasses import dataclass, field
from kiota_abstractions.serialization import AdditionalDataHolder, Parsable, ParseNode, SerializationWriter
from typing import Any, Optional, TYPE_CHECKING, Union

if TYPE_CHECKING:
    from .qyl.domains.ops.deployment.deployment_status import DeploymentStatus

@dataclass
class DeploymentUpdate(AdditionalDataHolder, Parsable):
    """
    Deployment update request
    """
    # Stores additional data not described in the OpenAPI description found when deserializing. Can be used for serialization as well.
    additional_data: dict[str, Any] = field(default_factory=dict)

    # Error message
    error_message: Optional[str] = None
    # Healthy replicas
    healthy_replicas: Optional[int] = None
    # New status
    status: Optional[DeploymentStatus] = None
    
    @staticmethod
    def create_from_discriminator_value(parse_node: ParseNode) -> DeploymentUpdate:
        """
        Creates a new instance of the appropriate class based on discriminator value
        param parse_node: The parse node to use to read the discriminator value and create the object
        Returns: DeploymentUpdate
        """
        if parse_node is None:
            raise TypeError("parse_node cannot be null.")
        return DeploymentUpdate()
    
    def get_field_deserializers(self,) -> dict[str, Callable[[ParseNode], None]]:
        """
        The deserialization information for the current model
        Returns: dict[str, Callable[[ParseNode], None]]
        """
        from .qyl.domains.ops.deployment.deployment_status import DeploymentStatus

        from .qyl.domains.ops.deployment.deployment_status import DeploymentStatus

        fields: dict[str, Callable[[Any], None]] = {
            "error_message": lambda n : setattr(self, 'error_message', n.get_str_value()),
            "healthy_replicas": lambda n : setattr(self, 'healthy_replicas', n.get_int_value()),
            "status": lambda n : setattr(self, 'status', n.get_enum_value(DeploymentStatus)),
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
        writer.write_str_value("error_message", self.error_message)
        writer.write_int_value("healthy_replicas", self.healthy_replicas)
        writer.write_enum_value("status", self.status)
        writer.write_additional_data_value(self.additional_data)
    

