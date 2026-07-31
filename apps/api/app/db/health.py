from sqlalchemy import text
from sqlalchemy.exc import SQLAlchemyError
from sqlalchemy.ext.asyncio import async_sessionmaker


class DatabaseReadinessCheck:
    name = "postgres"

    def __init__(self, session_factory: async_sessionmaker) -> None:
        self._session_factory = session_factory

    async def check(self) -> None:
        try:
            async with self._session_factory() as session:
                await session.execute(text("SELECT 1"))
        except SQLAlchemyError as exc:
            raise ConnectionError("database is unavailable") from exc
