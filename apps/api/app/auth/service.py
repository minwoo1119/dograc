import base64
import hashlib
import hmac
import json
import time
import uuid

from sqlalchemy.ext.asyncio import AsyncSession

from app.auth.errors import (
    InvalidCredentialsError,
    UnauthorizedError,
    UserAlreadyExistsError,
    UserNotFoundError,
)
from app.auth.repository import UserRepository
from app.auth.schemas import AuthResponse, LoginRequest, RegisterRequest, UserResponse
from app.db.models.user import User, hash_password

SECRET_KEY = "dograc_auth_secret_key_change_in_production"


def create_access_token(user_id: uuid.UUID, expires_in_seconds: int = 86400 * 7) -> str:
    """Create a tamper-proof HMAC-SHA256 signed bearer token."""
    payload = {
        "sub": str(user_id),
        "exp": int(time.time()) + expires_in_seconds,
    }
    raw_payload = json.dumps(payload, separators=(",", ":")).encode("utf-8")
    b64_payload = base64.urlsafe_b64encode(raw_payload).decode("utf-8").rstrip("=")
    
    signature = hmac.new(
        SECRET_KEY.encode("utf-8"),
        b64_payload.encode("utf-8"),
        hashlib.sha256,
    ).hexdigest()
    
    return f"{b64_payload}.{signature}"


def verify_access_token(token: str) -> uuid.UUID:
    """Verify bearer token signature and expiration, returning user UUID."""
    parts = token.split(".")
    if len(parts) != 2:
        raise UnauthorizedError("유효하지 않은 토큰 형식입니다.")
    
    b64_payload, signature = parts
    expected_sig = hmac.new(
        SECRET_KEY.encode("utf-8"),
        b64_payload.encode("utf-8"),
        hashlib.sha256,
    ).hexdigest()
    
    if not hmac.compare_digest(signature, expected_sig):
        raise UnauthorizedError("토큰 서명이 유효하지 않습니다.")
    
    try:
        # Add padding back if necessary
        padding = "=" * ((4 - len(b64_payload) % 4) % 4)
        raw_payload = base64.urlsafe_b64decode((b64_payload + padding).encode("utf-8"))
        data = json.loads(raw_payload.decode("utf-8"))
    except Exception as e:
        raise UnauthorizedError("토큰 데이터를 읽을 수 없습니다.") from e
    
    if data.get("exp", 0) < time.time():
        raise UnauthorizedError("토큰이 만료되었습니다. 다시 로그인해 주세요.")
    
    sub = data.get("sub")
    if not sub:
        raise UnauthorizedError("토큰에 사용자 식별자가 없습니다.")
    
    try:
        return uuid.UUID(sub)
    except ValueError as e:
        raise UnauthorizedError("토큰의 사용자 식별자가 올바르지 않습니다.") from e


class AuthService:
    def __init__(self, session: AsyncSession) -> None:
        self._repo = UserRepository(session)

    async def register(self, req: RegisterRequest) -> AuthResponse:
        email = req.email.strip().lower()
        username = req.username.strip()

        # 중복 체크
        if await self._repo.get_by_email(email):
            raise UserAlreadyExistsError("이미 가입된 이메일 주소입니다.")
        if await self._repo.get_by_username(username):
            raise UserAlreadyExistsError("이미 사용 중인 사용자명입니다.")

        pwd_hash, salt = hash_password(req.password)
        user = await self._repo.create(
            email=email,
            username=username,
            password_hash=pwd_hash,
            salt=salt,
        )

        token = create_access_token(user.id)
        return AuthResponse(
            access_token=token,
            user=UserResponse.model_validate(user),
        )

    async def login(self, req: LoginRequest) -> AuthResponse:
        identifier = req.email_or_username.strip().lower()
        user = await self._repo.get_by_email_or_username(identifier)
        if not user:
            # 이메일 대소문자 방어
            user = await self._repo.get_by_username(req.email_or_username.strip())

        if not user or not user.verify_password(req.password):
            raise InvalidCredentialsError()

        token = create_access_token(user.id)
        return AuthResponse(
            access_token=token,
            user=UserResponse.model_validate(user),
        )

    async def get_me(self, user_id: uuid.UUID) -> UserResponse:
        user = await self._repo.get_by_id(user_id)
        if not user:
            raise UserNotFoundError()
        return UserResponse.model_validate(user)
