create table if not exists public.time_accounts (
    user_id uuid primary key references auth.users(id) on delete cascade,
    payload jsonb not null default '{}'::jsonb,
    created_at timestamptz not null default now(),
    updated_at timestamptz not null default now()
);

create or replace function public.set_updated_at()
returns trigger
language plpgsql
as $$
begin
    new.updated_at = now();
    return new;
end;
$$;

drop trigger if exists time_accounts_set_updated_at on public.time_accounts;
create trigger time_accounts_set_updated_at
before update on public.time_accounts
for each row execute function public.set_updated_at();

alter table public.time_accounts enable row level security;

drop policy if exists "Users can read own time accounts" on public.time_accounts;
create policy "Users can read own time accounts"
on public.time_accounts
for select
using (auth.uid() = user_id);

drop policy if exists "Users can insert own time accounts" on public.time_accounts;
create policy "Users can insert own time accounts"
on public.time_accounts
for insert
with check (auth.uid() = user_id);

drop policy if exists "Users can update own time accounts" on public.time_accounts;
create policy "Users can update own time accounts"
on public.time_accounts
for update
using (auth.uid() = user_id)
with check (auth.uid() = user_id);

drop policy if exists "Users can delete own time accounts" on public.time_accounts;
create policy "Users can delete own time accounts"
on public.time_accounts
for delete
using (auth.uid() = user_id);

do $$
begin
    if not exists (
        select 1
        from pg_publication_tables
        where pubname = 'supabase_realtime'
          and schemaname = 'public'
          and tablename = 'time_accounts'
    ) then
        execute 'alter publication supabase_realtime add table public.time_accounts';
    end if;
end;
$$;
