# ClamAV development limits

The Compose service keeps `clamd` private and aligns its input limit with the API's
20 MB maximum. Expanded/container scans are capped at 100 MB, 16 levels, and 500 files.
ClamAV needs at least 3 GiB of host memory; 4 GiB is preferred while signatures update.
