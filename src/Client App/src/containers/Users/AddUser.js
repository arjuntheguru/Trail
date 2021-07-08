import React from "react";
import { Modal } from "react-bootstrap";
import UserForm from "./UserForm";

const AddUser = ({ show, onClose, siteInfo, data = {}, pageNumber }) => {
	return (
		<Modal show={show} onHide={onClose} centered size="lg">
			{
				<UserForm
					onClose={onClose}
					siteInfo={siteInfo}
					data={data}
					pageNumber={pageNumber}
				/>
			}
		</Modal>
	);
};

export default AddUser;
