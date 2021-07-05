import React from "react";
import { useDispatch } from "react-redux";
import { Pagination } from "react-bootstrap";

const TablePagination = ({
	api,
	currentPageNumber,
	totalRecords,
	callback,
	pageNumberChange,
}) => {
	const dispatch = useDispatch();

	const lastPage = Math.ceil(totalRecords / 10);
	console.log(totalRecords);
	const firstPageHandler = () => {
		pageNumberChange(1);
		dispatch(callback({ pageNumber: 1, pageSize: 10 }));
	};

	const lastPageHandler = () => {
		pageNumberChange(lastPage);
		dispatch(callback({ pageNumber: lastPage, pageSize: 10 }));
	};

	const previousPageHandler = () => {
		pageNumberChange(currentPageNumber - 1);
		dispatch(callback({ pageNumber: currentPageNumber - 1, pageSize: 10 }));
	};

	const nextPageHandler = () => {
		pageNumberChange(currentPageNumber + 1);
		dispatch(callback({ pageNumber: currentPageNumber + 1, pageSize: 10 }));
	};

	return (
		<Pagination>
			<Pagination.First
				disabled={totalRecords <= 0 ? true : false}
				onClick={firstPageHandler}
			/>
			<Pagination.Prev
				disabled={currentPageNumber <= 1 ? true : false}
				onClick={previousPageHandler}
			/>
			<Pagination.Item>
				Page {totalRecords <= 0 ? 0 : currentPageNumber} of {lastPage}
			</Pagination.Item>
			<Pagination.Next
				disabled={currentPageNumber <= 1 ? false : true}
				onClick={nextPageHandler}
			/>
			<Pagination.Last
				disabled={totalRecords <= 0 ? true : false}
				onClick={lastPageHandler}
			/>
		</Pagination>
	);
};

export default TablePagination;
