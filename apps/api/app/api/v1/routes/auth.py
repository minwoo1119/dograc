from fastapi import APIRouter, status

from app.api.dependencies import CurrentUserId, DatabaseSession
from app.auth.schemas import AuthResponse, LoginRequest, RegisterRequest, UserResponse
from app.auth.service import AuthService

router = APIRouter()


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
    service = AuthService(session)
    return await service.register(req)


@router.post(
    "/login",
    response_model=AuthResponse,
    summary="로그인",
)
async def login(
    req: LoginRequest,
    session: DatabaseSession,
) -> AuthResponse:
    service = AuthService(session)
    return await service.login(req)


@router.get(
    "/me",
    response_model=UserResponse,
    summary="내 정보 조회",
)
async def get_me(
    current_user_id: CurrentUserId,
    session: DatabaseSession,
) -> UserResponse:
    service = AuthService(session)
    return await service.get_me(current_user_id)


@router.post(
    "/logout",
    summary="로그아웃",
)
async def logout() -> dict[str, str]:
    return {"message": "정상적으로 로그아웃되었습니다."}
