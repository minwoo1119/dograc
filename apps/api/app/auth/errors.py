from fastapi import status

from app.core.errors import ApplicationError


class UserNotFoundError(ApplicationError):
    def __init__(self) -> None:
        super().__init__(
            code="USER_NOT_FOUND",
            message="사용자를 찾을 수 없습니다.",
            status_code=status.HTTP_404_NOT_FOUND,
        )


class UserAlreadyExistsError(ApplicationError):
    def __init__(self, message: str = "이미 등록된 이메일 또는 사용자명입니다.") -> None:
        super().__init__(
            code="USER_ALREADY_EXISTS",
            message=message,
            status_code=status.HTTP_409_CONFLICT,
        )


class InvalidCredentialsError(ApplicationError):
    def __init__(self) -> None:
        super().__init__(
            code="INVALID_CREDENTIALS",
            message="아이디 또는 비밀번호가 일치하지 않습니다.",
            status_code=status.HTTP_401_UNAUTHORIZED,
        )


class UnauthorizedError(ApplicationError):
    def __init__(self, message: str = "로그인이 필요한 요청입니다.") -> None:
        super().__init__(
            code="UNAUTHORIZED",
            message=message,
            status_code=status.HTTP_401_UNAUTHORIZED,
        )
