from __future__ import annotations
from collections.abc import Callable
from dataclasses import dataclass, field
from kiota_abstractions.serialization import AdditionalDataHolder, Parsable, ParseNode, SerializationWriter
from typing import Any, Optional, TYPE_CHECKING, Union

if TYPE_CHECKING:
    from .dora_performance_level import DoraPerformanceLevel

@dataclass
class DoraMetrics(AdditionalDataHolder, Parsable):
    """
    DORA metrics response
    """
    # Stores additional data not described in the OpenAPI description found when deserializing. Can be used for serialization as well.
    additional_data: dict[str, Any] = field(default_factory=dict)

    # Change failure rate
    change_failure_rate: Optional[float] = None
    # Deployment frequency (per day)
    deployment_frequency: Optional[float] = None
    # Lead time for changes (hours)
    lead_time_hours: Optional[float] = None
    # Mean time to recovery (hours)
    mttr_hours: Optional[float] = None
    # Performance level
    performance_level: Optional[DoraPerformanceLevel] = None
    
    @staticmethod
    def create_from_discriminator_value(parse_node: ParseNode) -> DoraMetrics:
        """
        Creates a new instance of the appropriate class based on discriminator value
        param parse_node: The parse node to use to read the discriminator value and create the object
        Returns: DoraMetrics
        """
        if parse_node is None:
            raise TypeError("parse_node cannot be null.")
        return DoraMetrics()
    
    def get_field_deserializers(self,) -> dict[str, Callable[[ParseNode], None]]:
        """
        The deserialization information for the current model
        Returns: dict[str, Callable[[ParseNode], None]]
        """
        from .dora_performance_level import DoraPerformanceLevel

        from .dora_performance_level import DoraPerformanceLevel

        fields: dict[str, Callable[[Any], None]] = {
            "change_failure_rate": lambda n : setattr(self, 'change_failure_rate', n.get_float_value()),
            "deployment_frequency": lambda n : setattr(self, 'deployment_frequency', n.get_float_value()),
            "lead_time_hours": lambda n : setattr(self, 'lead_time_hours', n.get_float_value()),
            "mttr_hours": lambda n : setattr(self, 'mttr_hours', n.get_float_value()),
            "performance_level": lambda n : setattr(self, 'performance_level', n.get_enum_value(DoraPerformanceLevel)),
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
        writer.write_float_value("change_failure_rate", self.change_failure_rate)
        writer.write_float_value("deployment_frequency", self.deployment_frequency)
        writer.write_float_value("lead_time_hours", self.lead_time_hours)
        writer.write_float_value("mttr_hours", self.mttr_hours)
        writer.write_enum_value("performance_level", self.performance_level)
        writer.write_additional_data_value(self.additional_data)
    

