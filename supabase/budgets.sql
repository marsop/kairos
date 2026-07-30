create table if not exists public.budgets (
    id uuid not null,
    user_id uuid not null references auth.users(id) on delete cascade,
    activity_id uuid not null,
    allocated_time_span bigint not null default 0,
    minimum_time_span bigint not null default 0,
    budget_type integer not null default 0,
    created_at timestamptz not null default now(),
    updated_at timestamptz not null default now(),
    primary key (user_id, id)
);

create index if not exists budgets_user_activity_type_idx
    on public.budgets (user_id, activity_id, budget_type);

drop trigger if exists budgets_set_updated_at on public.budgets;
create trigger budgets_set_updated_at
before update on public.budgets
for each row execute function public.set_updated_at();

alter table public.budgets enable row level security;

drop policy if exists "Users can read own budgets" on public.budgets;
create policy "Users can read own budgets"
on public.budgets
for select
using (auth.uid() = user_id);

drop policy if exists "Users can insert own budgets" on public.budgets;
create policy "Users can insert own budgets"
on public.budgets
for insert
with check (auth.uid() = user_id);

drop policy if exists "Users can update own budgets" on public.budgets;
create policy "Users can update own budgets"
on public.budgets
for update
using (auth.uid() = user_id)
with check (auth.uid() = user_id);

drop policy if exists "Users can delete own budgets" on public.budgets;
create policy "Users can delete own budgets"
on public.budgets
for delete
using (auth.uid() = user_id);

do $$
begin
    if not exists (
        select 1
        from pg_publication_tables
        where pubname = 'supabase_realtime'
          and schemaname = 'public'
          and tablename = 'budgets'
    ) then
        execute 'alter publication supabase_realtime add table public.budgets';
    end if;
end;
$$;
