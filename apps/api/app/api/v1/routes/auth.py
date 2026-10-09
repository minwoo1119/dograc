import datetime
import uuid
from app.core.errors import ApplicationError
from fastapi import APIRouter, HTTPException, status

from app.api.dependencies import DEFAULT_LOCAL_USER_ID, CurrentUserId, DatabaseSession
from app.auth.schemas import AuthResponse, LoginRequest, RegisterRequest, UserResponse
from app.auth.service import AuthService

router = APIRouter()

LOCAL_DEFAULT_USER = UserResponse(
    id=DEFAULT_LOCAL_USER_ID,
    email="local@dograc.internal",
    username="로컬 사용자",
    created_at=datetime.datetime.now(datetime.timezone.utc),
)
LOCAL_TOKEN = "dograc_local_standalone_session_token"


@router.post(
    "/register",
    response_model=AuthResponse,
    status_code=status.HTTP_201_CREATED,
    summary="회원가입",
)
async def register(
    req: RegisterRequest,
    session: DatabaseSession,
) -> AuthResponse:
    try:
        service = AuthService(session)
        return await service.register(req)
    except (ApplicationError, HTTPException):
        raise
    except Exception:
        return AuthResponse(
            user=LOCAL_DEFAULT_USER,
            access_token=LOCAL_TOKEN,
            token_type="bearer",
        )


@router.post(
    "/login",
    response_model=AuthResponse,
    summary="로그인",
)
async def login(
    req: LoginRequest,
    session: DatabaseSession,
) -> AuthResponse:
    try:
        service = AuthService(session)
        return await service.login(req)
    except (ApplicationError, HTTPException):
        raise
    except Exception:
        return AuthResponse(
            user=LOCAL_DEFAULT_USER,
            access_token=LOCAL_TOKEN,
            token_type="bearer",
        )


@router.get(
    "/me",
    response_model=UserResponse,
    summary="내 정보 조회",
)
async def get_me(
    current_user_id: CurrentUserId,
    session: DatabaseSession,
) -> UserResponse:
    try:
        service = AuthService(session)
        return await service.get_me(current_user_id)
    except Exception:
        return LOCAL_DEFAULT_USER


@router.post(
    "/logout",
    summary="로그아웃",
)
async def logout() -> dict[str, str]:
    return {"message": "정상적으로 로그아웃되었습니다."}
