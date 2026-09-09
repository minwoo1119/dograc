"""create users table

Revision ID: 20260909_0006
Revises: 20260909_0005
Create Date: 2026-09-09
"""

from collections.abc import Sequence
import hashlib
import uuid

import sqlalchemy as sa
from alembic import op

revision: str = "20260909_0006"
down_revision: str | None = "20260909_0005"
branch_labels: str | Sequence[str] | None = None
depends_on: str | Sequence[str] | None = None


def _hash(pwd: str, salt: str) -> str:
    return hashlib.pbkdf2_hmac(
        "sha256",
        pwd.encode("utf-8"),
        salt.encode("utf-8"),
        iterations=100_000,
    ).hex()


def upgrade() -> None:
    users_table = op.create_table(
        "users",
        sa.Column("id", sa.Uuid(), nullable=False),
        sa.Column("email", sa.String(length=255), nullable=False),
        sa.Column("username", sa.String(length=100), nullable=False),
        sa.Column("password_hash", sa.String(length=255), nullable=False),
        sa.Column("salt", sa.String(length=64), nullable=False),
        sa.Column(
            "created_at",
            sa.DateTime(timezone=True),
            server_default=sa.func.now(),
            nullable=False,
        ),
        sa.Column(
            "updated_at",
            sa.DateTime(timezone=True),
            server_default=sa.func.now(),
            nullable=False,
        ),
        sa.PrimaryKeyConstraint("id"),
    )
    op.create_index("ix_users_email", "users", ["email"], unique=True)
    op.create_index("ix_users_username", "users", ["username"], unique=True)

    # 기본 데모 계정 시드 삽입
    default_salt = "dograc_seed_salt_admin"
    op.bulk_insert(
        users_table,
        [
            {
                "id": uuid.UUID("00000000-0000-0000-0000-000000000001"),
                "email": "admin@dograc.io",
                "username": "admin",
                "password_hash": _hash("password123", default_salt),
                "salt": default_salt,
            }
        ],
    )


def downgrade() -> None:
    op.drop_index("ix_users_username", table_name="users")
    op.drop_index("ix_users_email", table_name="users")
    op.drop_table("users")
