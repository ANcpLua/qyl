from __future__ import annotations
from collections.abc import Callable
from dataclasses import dataclass, field
from kiota_abstractions.base_request_builder import BaseRequestBuilder
from kiota_abstractions.base_request_configuration import RequestConfiguration
from kiota_abstractions.default_query_parameters import QueryParameters
from kiota_abstractions.get_path_parameters import get_path_parameters
from kiota_abstractions.method import Method
from kiota_abstractions.request_adapter import RequestAdapter
from kiota_abstractions.request_information import RequestInformation
from kiota_abstractions.request_option import RequestOption
from kiota_abstractions.serialization import Parsable, ParsableFactory
from typing import Any, Optional, TYPE_CHECKING, Union
from warnings import warn

if TYPE_CHECKING:
    from .item.with_trace_item_request_builder import WithTraceItemRequestBuilder

class TracesRequestBuilder(BaseRequestBuilder):
    """
    Builds and executes requests for operations under /v1/stream/traces
    """
    def __init__(self,request_adapter: RequestAdapter, path_parameters: Union[str, dict[str, Any]]) -> None:
        """
        Instantiates a new TracesRequestBuilder and sets the default values.
        param path_parameters: The raw url or the url-template parameters for the request.
        param request_adapter: The request adapter to use to execute the requests.
        Returns: None
        """
        super().__init__(request_adapter, "{+baseurl}/v1/stream/traces{?minDurationMs,serviceName}", path_parameters)
    
    def by_trace_id(self,trace_id: str) -> WithTraceItemRequestBuilder:
        """
        Gets an item from the ApiSdk.v1.stream.traces.item collection
        param trace_id: Unique identifier of the item
        Returns: WithTraceItemRequestBuilder
        """
        if trace_id is None:
            raise TypeError("trace_id cannot be null.")
        from .item.with_trace_item_request_builder import WithTraceItemRequestBuilder

        url_tpl_params = get_path_parameters(self.path_parameters)
        url_tpl_params["traceId"] = trace_id
        return WithTraceItemRequestBuilder(self.request_adapter, url_tpl_params)
    
    async def get(self,request_configuration: Optional[RequestConfiguration[TracesRequestBuilderGetQueryParameters]] = None) -> Optional[bytes]:
        """
        Stream traces in real-time
        param request_configuration: Configuration for the request such as headers, query parameters, and middleware options.
        Returns: bytes
        """
        request_info = self.to_get_request_information(
            request_configuration
        )
        if not self.request_adapter:
            raise Exception("Http core is null") 
        return await self.request_adapter.send_primitive_async(request_info, "bytes", None)
    
    def to_get_request_information(self,request_configuration: Optional[RequestConfiguration[TracesRequestBuilderGetQueryParameters]] = None) -> RequestInformation:
        """
        Stream traces in real-time
        param request_configuration: Configuration for the request such as headers, query parameters, and middleware options.
        Returns: RequestInformation
        """
        request_info = RequestInformation(Method.GET, self.url_template, self.path_parameters)
        request_info.configure(request_configuration)
        request_info.headers.try_add("Accept", "text/event-stream")
        return request_info
    
    def with_url(self,raw_url: str) -> TracesRequestBuilder:
        """
        Returns a request builder with the provided arbitrary URL. Using this method means any other path or query parameters are ignored.
        param raw_url: The raw URL to use for the request builder.
        Returns: TracesRequestBuilder
        """
        if raw_url is None:
            raise TypeError("raw_url cannot be null.")
        return TracesRequestBuilder(self.request_adapter, raw_url)
    
    @dataclass
    class TracesRequestBuilderGetQueryParameters():
        """
        Stream traces in real-time
        """
        def get_query_parameter(self,original_name: str) -> str:
            """
            Maps the query parameters names to their encoded names for the URI template parsing.
            param original_name: The original query parameter name in the class.
            Returns: str
            """
            if original_name is None:
                raise TypeError("original_name cannot be null.")
            if original_name == "min_duration_ms":
                return "minDurationMs"
            if original_name == "service_name":
                return "serviceName"
            return original_name
        
        # Minimum duration filter
        min_duration_ms: Optional[int] = None

        # Service name filter
        service_name: Optional[str] = None

    
    @dataclass
    class TracesRequestBuilderGetRequestConfiguration(RequestConfiguration[TracesRequestBuilderGetQueryParameters]):
        """
        Configuration for the request such as headers, query parameters, and middleware options.
        """
        warn("This class is deprecated. Please use the generic RequestConfiguration class generated by the generator.", DeprecationWarning)
    

