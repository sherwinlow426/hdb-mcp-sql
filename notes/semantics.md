# Schema semantics — working notes

## 1. The methodology break (the hero example)
Before March 2012 rows are dated by APPROVAL date.
From March 2012 onward they are dated by REGISTRATION date.
A query comparing 2011 to 2013 silently compares two different things.

## 2. remaining_lease format varies
Absent in the oldest files; plain year count in one vintage;
"61 years 04 months" string from 2017. Normalise to decimal years.

## 3. storey_range is a bucket, not a number
"10 TO 12". Any average over it is wrong unless you say what you did.

## 4. resale_price is nominal
"Did prices rise 2015 to 2025" has a different answer in real terms.

## 5. flat_model is long-tailed and historically inconsistent

## 6. Five datasets, differing schemas, must be unioned