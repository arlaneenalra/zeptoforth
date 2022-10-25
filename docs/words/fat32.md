# FAT32 Support

zeptoforth includes FAT32 filesystem support combined with MBR partition table support on devices implementing the `<block-dev>` class defined in the module `block-dev` (currently only the `<sd>` class defined in the module `sd`). It supports creating files and directories, reading files and directories, writing files and (indirectly) directories, seeking in files, removing files and (empty) directories, and renaming (but currently not moving) files and directories. It also supports parsing paths within filesystems. It supports reading partition table entries from the MBR, and uses these when initializing FAT32 filesystems.

### `fat32`

The `fat32` module contains the following words:

##### `x-sector-size-not-supported`
( -- )

Sector size exception.
  
##### `x-fs-version-not-supported`
( -- )

Filesystem version not supported exception.
  
##### `x-bad-info-sector`
( -- )

Bad info sector exception.
  
##### `x-no-clusters-free`
( -- )

No clusters free exception.
  
##### `x-file-name-format`
( -- )

Unsupported file name format exception.
  
##### `x-out-of-range-entry`
( -- )

Out of range directory entry index exception.
  
##### `x-out-of-range-partition`
( -- )

Out of range partition index exception.
  
##### `x-entry-not-found`
( -- )

Directory entry not found exception.
  
##### `x-entry-already-exists`
( -- )

Directory entry already exists exception.
  
##### `x-entry-not-file`
( -- )

Directory entry is not a file exception.
  
##### `x-entry-not-dir`
( -- )

Directory entry is not a directory exception.
  
##### `x-dir-is-not-empty`
( -- )

Directory is not empty exception.
  
##### `x-forbidden-dir`
( -- )

Directory name being changed or set is forbidden exception.
  
##### `x-empty-path`
( -- )

No file or directory referred to in path within directory exception.
  
##### `x-invalid-path`
( -- )

Invalid path exception.

##### `seek-set`
( -- whence )

Seek from the beginning of a file.

##### `seek-cur`
( -- whence )

Seek from the current position in a file

##### `seek-end`
( -- whence )

##### `<mbr>`

The master boot record class. This class is used to read a partition entry from for initializing a FAT32 filesystem.

The `<mbr>` class includes the following constructor:

##### `new`
( mbr-device mbr -- )

Construct an `<mbr>` instance with the block device *mbr-device*.

The `<mbr>` class includes the following methods:

##### `mbr-valid?`
( mbr -- valid? )

Is the MBR valid?

##### `partition@`
( partition index mbr -- )

Read a partition entry, with an index from 0 to 3.

##### `partition!`
( partition index mbr -- )

Write a partition entry, with an index from 0 to 3.

##### `<partition>`

The master boot record partition entry class. This class is used to read partition entries from the MBR partition table for initializing FAT32 filesystems.

The `<partition>` class includes the following members:

##### `partition-active`
( partition -- partition-active )

Partition active state.
    
##### `partition-type`
( partition -- partition-type )

Partition type.
    
##### `partition-first-sector`
( partition -- partition-first-sector )

Partition first sector index.
    
##### `partition-sectors`
( partition -- partition-sectors )

Partition sectors.
    
##### `partition-active?`
( partition -- active? )

Is the partition active?

##### `<fat32-fs>`
( -- class )

The FAT32 filesystem class.

The `<fat32-fs>` class includes the following constructor:

##### `new`
( partition device fs -- )

Construct an instance of the `<fat32-fs>` class with block device *device* and MBR partition entry *partition*. Note that after executing this the filesystem will be ready for use, and the block device must be in working order at this time.

##### `root-dir@`
( dir fs -- )

Initialize a root directory of a FAT32 filesystem; the directory object need not be initialized already, but if it is no harm will result.

##### `with-root-path`
( c-addr u xt fs ) ( xt: c-addr' u' dir -- )

Parse a path starting at the root directory of a FAT32 filesystem, and pass the leaf's name along with a directory object containing that leaf (or which would contain said leaf if it did not exist already) to the passed in *xt*. Note that said directory object will be destroyed when *xt* returns.

##### `<fat32-file>`
( --  class )

The FAT32 file class.

The `<fat32-file>` class includes the following constructor:

##### `new`
( fs file -- )

Construct an instance of `<fat32-file>` with the FAT32 filesystem *fs*.

The `<fat32-file>` class includes the following methods:

##### `read-file`
( c-addr u file -- bytes )

Read data from a file.
    
##### `write-file`
( c-addr u file -- bytes )

Write data to a file.
    
##### `truncate-file`
( file -- )

Truncate a file.
    
##### `seek-file`
( offset whence file -- )

Seek in a file.
    
##### `tell-file`
( file -- offset )

Get the current offset in a file.
      
##### `file-size@`
( file -- bytes )

Get the size of a file.

##### `<fat32-dir>`

The FAT32 directory class.

The `<fat32-dir>` class includes the following constructor:

##### `new`
( fs file -- )

Construct an instance of `<fat32-dir>` with the FAT32 filesystem *fs*.

The `<fat32-dir>` class includes the following methods:

##### `with-path`
( c-addr u xt dir -- ) ( xt: c-addr' u' dir' -- )

Parse a path starting at a given directory, and pass the leaf's name along with a directory object containing that leaf (or which would contain said leaf if it did not exist already) to the passed in *xt*. Note that said directory object will be destroyed when *xt* returns unless it was the original directory object passed in.

##### `read-dir`
( entry dir -- entry-read? )

Read an entry from a directory, and return whether an entry was read.
    
##### `create-file`
( c-addr u new-file dir -- )

Create a file. Note that *new-file* need not be initialized prior to use, but no harm is done if it is.
    
##### `open-file`
( c-addr u opened-file dir -- )

Open a file. Note that *opened-file* need not be initialized prior to use, but no harm is done if it is.
    
##### `remove-file`
( c-addr u dir -- )

Remove a file.
    
##### `create-dir`
( c-addr u new-dir dir -- )

Create a directory. Note that *new-dir* need not be initialized prior to use, but no harm is done if it is.
    
##### `open-dir`
( c-addr u opened-dir dir -- )

Open a directory. Note that *opened-dir* need not be initialized prior to use, but no harm is done if it is.
    
##### `remove-dir`
( c-addr u dir -- )

Remove a directory.
    
##### `rename`
( new-c-addr new-u c-addr u dir -- )

Rename a file or directory.
    
##### `dir-empty?`
( dir -- empty? )

Get whether a directory is empty.

##### `<fat32-entry>`
( -- class )

The FAT32 directory entry class.

The `<fat32-entry>` class has no constructor.

The `<fat32-entry>` class has the following members:

##### `short-file-name`
( entry -- short-file-name-addr )

This member is 8 bytes in size.

The short file name component, padded with spaces.

The first byte can have the special values:
$00: final entry in the directory entry table
$05: the initial byte is actually $35
$2E: the dot entry
$E5: the directory entry has been deleted

##### `short-file-ext`
( entry -- short-file-ext-addr )

This member is 3 bytes in size.

The short file extension component, padded with spaces.

##### `file-attributes`
( entry -- file-attributes-addr )

This member is 1 bytes in size.

The file attributes.

There are the following bits:
$01: read only
$02: hidden
$04: system (do not move in the filesystem)
$08: volume label
$10: subdirectory (subdirectories have a file size of zero)
$20: archive
$40: device
$80: reserved

##### `nt-vfat-case`
( entry -- nt-vfat-case-addr )

This member is 1 bytes in size.

Windows NT VFAT case information.

##### `create-time-fine`
( entry -- create-time-fine-addr )

This member is 1 bytes in size.

Create time file resolution, 10 ms increments, from 0 to 199.

##### `create-time-coarse`
( entry -- create-time-coarse-addr )

This member is 2 bytes in size.

Create time with coarse resolution, 2 s increments.

bits 15-11: hours (0-23)
bits 10-5: minutes (0-59)
bits 4-0: seconds / 2 (0-29)

##### `create-date`
( entry -- create-date-addr )

This member is 2 bytes in size.

Create date.

bits 15-9: year (0 = 1980)
bits 8-5: month (1-12)
bits 4-0: day (1-31)

##### `access-date`
( entry -- access-date-addr )

This member is 2 bytes in size.

Last access date.

bits 15-9: year (0 = 1980)
bits 8-5: month (1-12)
bits 4-0: day (1-31)

##### `first-cluster-high`
( entry -- first-cluster-high-addr )

This member is 2 bytes in size.

High two bytes of the first cluster number.

##### `modify-time-coarse`
( entry -- modify-time-coarse-addr )

This member is 2 bytes in size.

Last modify time with coarse resolution, 2 s increments.

bits 15-11: hours (0-23)
bits 10-5: minutes (0-59)
bits 4-0: seconds / 2 (0-29)

##### `modify-date`
( entry -- modify-date-addr )

This member is 2 bytes in size.

Last modify date.

bits 15-9: year (0 = 1980)
bits 8-5: month (1-12)
bits 4-0: day (1-31)

##### `first-cluster-low`
( entry -- first-cluster-low-addr )

This member is 2 bytes in size.

Low two bytes of the first cluster number.

##### `entry-file-size`
( entry -- entry-file-size-addr )

This member is 4 bytes in size.

The file size; is always 0 for subdirectories and volume labels.

##### `buffer>entry`
( addr entry -- )

Import a buffer into a directory entry.

##### `entry>buffer`
( addr entry -- )

Export a directory entry as a buffer.

##### `init-blank-entry`
( entry -- )

Initialize a blank directory entry.

##### `init-file-entry`
( file-size first-cluster c-addr u entry -- )

Initialize a file directory entry.

##### `init-dir-entry`
( first-cluster c-addr u entry -- )

Initialize a subdirectory directory entry.

##### `init-end-entry`
( entry -- )

Initialize an end directory entry.

##### `mark-entry-deleted`
( entry -- )

Mark a directory entry as deleted.

##### `entry-deleted?`
( entry -- deleted? )

Get whether a directory entry has been deleted.

##### `entry-end?`
( entry -- end? )

Get whether a directory entry is the last in a directory.

##### `entry-file?`
( entry -- file? )

Get whether a directory entry is for a file.

##### `entry-dir?`
( entry -- subdir? )

Get whether a directory entry is for a subdirectory.

##### `first-cluster@`
( entry -- cluster )

Get the first cluster index of a directory entry.

##### `first-cluster!`
( cluster entry -- )

Set the first cluster index of a directory entry.

##### `file-name!`
( c-addr u entry -- )

Set the file name of a directory entry, converted from a normal string.

##### `dir-name!`
( c-addr u entry -- )

Set the directory name of a directory entry, converted from a normal string.

##### `file-name@`
( c-addr u entry -- c-addr u' )

Get the file name of a directory entry, converted to a normal string.