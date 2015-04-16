/*
create table fact_table (
 dimension_value1 number,
 dimension_value2 number,
 any_value1 number,
 any_value2 number
);

create index idx_dim1 on fact_table ( dimension_value1 );
create index idx_dim2 on fact_table ( dimension_value2 );

insert into fact_table (dimension_value1, dimension_value2, any_value1, any_value2) values (1,2, 10, 20);

create table dimension_table (
 dimension_value number
);

insert into dimension_table (dimension_value) values (1);
*/

select fac.dimension_value, dim.dimension_value
from
(
  select dimension_value
  from fact_table
  unpivot (
    (dimension_value) for side in (
      (dimension_value1) as 'd', 
      (dimension_value2) as 'c'
    )
  )
) fac
join dimension_table dim on fac.dimension_value = dim.dimension_value
/

/*
drop table fact_table;
drop table dimension_table;
*/