from __future__ import annotations
from collections.abc import Callable
from kiota_abstractions.base_request_builder import BaseRequestBuilder
from kiota_abstractions.get_path_parameters import get_path_parameters
from kiota_abstractions.request_adapter import RequestAdapter
from typing import Any, Optional, TYPE_CHECKING, Union

if TYPE_CHECKING:
    from .deployments.deployments_request_builder import DeploymentsRequestBuilder
    from .events.events_request_builder import EventsRequestBuilder
    from .exceptions.exceptions_request_builder import ExceptionsRequestBuilder
    from .logs.logs_request_builder import LogsRequestBuilder
    from .metrics.metrics_request_builder import MetricsRequestBuilder
    from .traces.traces_request_builder import TracesRequestBuilder

class StreamRequestBuilder(BaseRequestBuilder):
    """
    Builds and executes requests for operations under /v1/stream
    """
    def __init__(self,request_adapter: RequestAdapter, path_parameters: Union[str, dict[str, Any]]) -> None:
        """
        Instantiates a new StreamRequestBuilder and sets the default values.
        param path_parameters: The raw url or the url-template parameters for the request.
        param request_adapter: The request adapter to use to execute the requests.
        Returns: None
        """
        super().__init__(request_adapter, "{+baseurl}/v1/stream", path_parameters)
    
    @property
    def deployments(self) -> DeploymentsRequestBuilder:
        """
        The deployments property
        """
        from .deployments.deployments_request_builder import DeploymentsRequestBuilder

        return DeploymentsRequestBuilder(self.request_adapter, self.path_parameters)
    
    @property
    def events(self) -> EventsRequestBuilder:
        """
        The events property
        """
        from .events.events_request_builder import EventsRequestBuilder

        return EventsRequestBuilder(self.request_adapter, self.path_parameters)
    
    @property
    def exceptions(self) -> ExceptionsRequestBuilder:
        """
        The exceptions property
        """
        from .exceptions.exceptions_request_builder import ExceptionsRequestBuilder

        return ExceptionsRequestBuilder(self.request_adapter, self.path_parameters)
    
    @property
    def logs(self) -> LogsRequestBuilder:
        """
        The logs property
        """
        from .logs.logs_request_builder import LogsRequestBuilder

        return LogsRequestBuilder(self.request_adapter, self.path_parameters)
    
    @property
    def metrics(self) -> MetricsRequestBuilder:
        """
        The metrics property
        """
        from .metrics.metrics_request_builder import MetricsRequestBuilder

        return MetricsRequestBuilder(self.request_adapter, self.path_parameters)
    
    @property
    def traces(self) -> TracesRequestBuilder:
        """
        The traces property
        """
        from .traces.traces_request_builder import TracesRequestBuilder

        return TracesRequestBuilder(self.request_adapter, self.path_parameters)
    

