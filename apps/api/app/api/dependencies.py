import uuid
from collections.abc import AsyncIterator
from typing import Annotated

from fastapi import Depends, Header, Request
from sqlalchemy.ext.asyncio import AsyncSession


async def get_db_session(request: Request) -> AsyncIterator[AsyncSession]:
    async with request.app.state.session_factory() as session:
        yield session


def get_current_user_id(
    user_id: Annotated[uuid.UUID, Header(alias="X-User-ID")],
) -> uuid.UUID:
    return user_id


DatabaseSession = Annotated[AsyncSession, Depends(get_db_session)]
CurrentUserId = Annotated[uuid.UUID, Depends(get_current_user_id)]
