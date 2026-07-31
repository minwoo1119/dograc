import asyncio
from typing import Protocol

from app.health.schemas import DependencyCheck


class ReadinessCheck(Protocol):
    @property
    def name(self) -> str: ...

    async def check(self) -> None: ...


async def _run_check(check: ReadinessCheck) -> DependencyCheck:
    try:
        await check.check()
    except (TimeoutError, ConnectionError, OSError) as exc:
        return DependencyCheck(name=check.name, healthy=False, detail=type(exc).__name__)
    return DependencyCheck(name=check.name, healthy=True)


async def run_readiness_checks(
    checks: tuple[ReadinessCheck, ...],
) -> list[DependencyCheck]:
    if not checks:
        return []
    return list(await asyncio.gather(*(_run_check(check) for check in checks)))
