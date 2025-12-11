from __future__ import annotations
import datetime
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
    from ...models.qyl.common.errors.internal_server_error import InternalServerError
    from ...models.qyl.common.errors.validation_error import ValidationError
    from .item.with_session_item_request_builder import WithSessionItemRequestBuilder
    from .sessions_get_response import SessionsGetResponse
    from .stats.stats_request_builder import StatsRequestBuilder

class SessionsRequestBuilder(BaseRequestBuilder):
    """
    Builds and executes requests for operations under /v1/sessions
    """
    def __init__(self,request_adapter: RequestAdapter, path_parameters: Union[str, dict[str, Any]]) -> None:
        """
        Instantiates a new SessionsRequestBuilder and sets the default values.
        param path_parameters: The raw url or the url-template parameters for the request.
        param request_adapter: The request adapter to use to execute the requests.
        Returns: None
        """
        super().__init__(request_adapter, "{+baseurl}/v1/sessions{?cursor,endTime,isActive,limit,startTime,userId}", path_parameters)
    
    def by_session_id(self,session_id: str) -> WithSessionItemRequestBuilder:
        """
        Gets an item from the ApiSdk.v1.sessions.item collection
        param session_id: Unique identifier of the item
        Returns: WithSessionItemRequestBuilder
        """
        if session_id is None:
            raise TypeError("session_id cannot be null.")
        from .item.with_session_item_request_builder import WithSessionItemRequestBuilder

        url_tpl_params = get_path_parameters(self.path_parameters)
        url_tpl_params["sessionId"] = session_id
        return WithSessionItemRequestBuilder(self.request_adapter, url_tpl_params)
    
    async def get(self,request_configuration: Optional[RequestConfiguration[SessionsRequestBuilderGetQueryParameters]] = None) -> Optional[SessionsGetResponse]:
        """
        List sessions
        param request_configuration: Configuration for the request such as headers, query parameters, and middleware options.
        Returns: Optional[SessionsGetResponse]
        """
        request_info = self.to_get_request_information(
            request_configuration
        )
        from ...models.qyl.common.errors.internal_server_error import InternalServerError
        from ...models.qyl.common.errors.validation_error import ValidationError

        error_mapping: dict[str, type[ParsableFactory]] = {
            "400": ValidationError,
            "500": InternalServerError,
        }
        if not self.request_adapter:
            raise Exception("Http core is null") 
        from .sessions_get_response import SessionsGetResponse

        return await self.request_adapter.send_async(request_info, SessionsGetResponse, error_mapping)
    
    def to_get_request_information(self,request_configuration: Optional[RequestConfiguration[SessionsRequestBuilderGetQueryParameters]] = None) -> RequestInformation:
        """
        List sessions
        param request_configuration: Configuration for the request such as headers, query parameters, and middleware options.
        Returns: RequestInformation
        """
        request_info = RequestInformation(Method.GET, self.url_template, self.path_parameters)
        request_info.configure(request_configuration)
        request_info.headers.try_add("Accept", "application/json")
        return request_info
    
    def with_url(self,raw_url: str) -> SessionsRequestBuilder:
        """
        Returns a request builder with the provided arbitrary URL. Using this method means any other path or query parameters are ignored.
        param raw_url: The raw URL to use for the request builder.
        Returns: SessionsRequestBuilder
        """
        if raw_url is None:
            raise TypeError("raw_url cannot be null.")
        return SessionsRequestBuilder(self.request_adapter, raw_url)
    
    @property
    def stats(self) -> StatsRequestBuilder:
        """
        The stats property
        """
        from .stats.stats_request_builder import StatsRequestBuilder

        return StatsRequestBuilder(self.request_adapter, self.path_parameters)
    
    @dataclass
    class SessionsRequestBuilderGetQueryParameters():
        """
        List sessions
        """
        def get_query_parameter(self,original_name: str) -> str:
            """
            Maps the query parameters names to their encoded names for the URI template parsing.
            param original_name: The original query parameter name in the class.
            Returns: str
            """
            if original_name is None:
                raise TypeError("original_name cannot be null.")
            if original_name == "end_time":
                return "endTime"
            if original_name == "is_active":
                return "isActive"
            if original_name == "start_time":
                return "startTime"
            if original_name == "user_id":
                return "userId"
            if original_name == "cursor":
                return "cursor"
            if original_name == "limit":
                return "limit"
            return original_name
        
        # Cursor
        cursor: Optional[str] = None

        # End time
        end_time: Optional[datetime.datetime] = None

        # Is active filter
        is_active: Optional[bool] = None

        # Page size
        limit: Optional[int] = None

        # Start time
        start_time: Optional[datetime.datetime] = None

        # User ID filter
        user_id: Optional[str] = None

    
    @dataclass
    class SessionsRequestBuilderGetRequestConfiguration(RequestConfiguration[SessionsRequestBuilderGetQueryParameters]):
        """
        Configuration for the request such as headers, query parameters, and middleware options.
        """
        warn("This class is deprecated. Please use the generic RequestConfiguration class generated by the generator.", DeprecationWarning)
    

